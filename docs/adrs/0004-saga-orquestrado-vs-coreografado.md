# ADR 0004 — Saga Orquestrado (no OS Service) vs. Coreografado

- **Status:** Aceito
- **Escopo:** global (todos os serviços da Fase 4)
- **Data:** 2026-10

## Contexto

Abrir uma ordem de serviço (OS) atravessa três serviços, cada um com banco próprio — não existe transação
ACID que cubra tudo:

1. **OS Service** abre a OS;
2. **Execução Service** faz o diagnóstico e publica `diagnostico.concluido`;
3. **Billing Service** gera o orçamento, aguarda a aprovação do cliente, depois cobra no Mercado Pago;
4. **Execução Service** executa o reparo (`orcamento.aprovado` dispara) e finaliza (`execucao.finalizada`);
5. **Billing Service** cobra; `pagamento.confirmado` fecha a OS.

Qualquer etapa pode falhar (cliente recusa, pagamento recusado, OS cancelada, serviço indisponível) e o
estado já gravado nas etapas anteriores precisa ser **compensado**. O PDF do Tech Challenge exige Saga com
rollback/compensação e a justificativa da escolha no README.

## Decisão

**Saga Orquestrado, com o orquestrador no OS Service.** O OS Service é dono do estado global da OS
(ele já guarda o status e o histórico), então é o ponto natural para decidir "qual o próximo passo" e
"qual compensação disparar". Billing e Execução são **participantes**: reagem a eventos, executam sua etapa
local e publicam o resultado (`orcamento.*`, `pagamento.*`). Eles não conhecem o fluxo inteiro.

### Passos e compensações

| Etapa (participante)                  | Evento de sucesso        | Evento de falha / desfecho negativo        | Compensação                                                                                           |
|---------------------------------------|--------------------------|--------------------------------------------|-------------------------------------------------------------------------------------------------------|
| Diagnóstico (Execução)                | `diagnostico.concluido`  | —                                          | —                                                                                                     |
| Orçamento (Billing)                   | `orcamento.gerado`       | mensagem vai à DLQ após 5 tentativas       | OS Service cancela a OS (alarme sobre a DLQ)                                                          |
| Aprovação do cliente (Billing)        | `orcamento.aprovado`     | `orcamento.recusado`                       | OS Service cancela a OS e publica `os.cancelada` → Billing cancela o orçamento                        |
| Execução (Execução)                   | `execucao.finalizada`    | falha na execução                          | OS Service publica `os.cancelada` → Billing cancela orçamento e, se houver, a cobrança pendente       |
| Cobrança (Billing)                    | `pagamento.confirmado`   | `pagamento.recusado`                       | OS Service decide (nova tentativa ou cancelamento)                                                    |
| Cancelamento tardio                   | —                        | `os.cancelada` com pagamento já confirmado | **Billing estorna** o pagamento no Mercado Pago e marca `Estornado`                                    |

Todos os passos do Billing são **idempotentes** (mensagens SQS são entregues ao menos uma vez) e publicam
os eventos via **Outbox** (ADR 0006), então o orquestrador nunca "perde" um desfecho.

## Consequências

**Positivas**
- Fluxo explícito e rastreável em um único lugar — mais fácil de depurar, demonstrar no vídeo e testar de ponta a ponta.
- Participantes simples e desacoplados entre si (Billing não chama Execução nem o contrário).
- Compensações centralizadas: uma regra nova (ex.: "cancelar se o orçamento não for aprovado em 7 dias") altera só o orquestrador.

**Negativas / custos**
- O OS Service concentra lógica de coordenação e vira ponto de atenção (precisa ser resiliente; seu estado de Saga é persistido).
- Acoplamento dos participantes ao *vocabulário* de eventos definido pelo orquestrador (mitigado por `docs/events.md`).

## Alternativas descartadas

- **Coreografado (só eventos entre serviços):** mais desacoplado, sem ponto central — mas o fluxo fica espalhado
  entre três repositórios, a compensação depende de cada serviço "adivinhar" quem reage a quê, e rastrear *por que*
  uma OS está parada exige juntar logs de todos. Com 3 serviços e um fluxo linear, o ganho de desacoplamento não compensa a perda de rastreabilidade.
- **2PC / transação distribuída:** inviável com bancos independentes e mensageria assíncrona; reduz disponibilidade.

## Referências
- Chris Richardson, *Microservices Patterns* — Saga (orchestration vs. choreography): https://microservices.io/patterns/data/saga.html
- Enunciado Tech Challenge Fase 4 — "Implementação do Saga Pattern".
