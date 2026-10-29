# Contratos de eventos — Billing Service

Todos os eventos trafegam em SNS→SQS (ADR 0005). Nomes de tópico: `oficina-{env}-{evento com hifens}`; ARN em
`/oficina/{env}/messaging/topics/{evento}/arn` (SSM).

## Envelope publicado pelo Billing

```json
{
  "eventId": "0b9f1c6e-…",
  "eventType": "pagamento.confirmado",
  "eventVersion": "1.0",
  "occurredAt": "2026-10-23T14:03:11Z",
  "correlationId": "7f3c…",
  "producer": "billing-service",
  "data": { "pagamentoId": "…", "osId": "…" }
}
```

`correlationId` também é enviado como MessageAttribute SNS. `data.osId` segue o contrato compartilhado; a API REST continua expondo `ordemServicoId`.

## Eventos que o Billing PUBLICA

| Tópico                 | `data`                                            | Quando                                                         |
|------------------------|---------------------------------------------------|----------------------------------------------------------------|
| `orcamento.gerado`     | `orcamentoId`, `osId`, `valorTotal`               | Orçamento criado a partir do diagnóstico                       |
| `orcamento.aprovado`   | `orcamentoId`, `osId`                             | Cliente aprovou → **dispara a execução**                       |
| `orcamento.recusado`   | `orcamentoId`, `osId`                             | Cliente recusou → orquestrador compensa                        |
| `pagamento.confirmado` | `pagamentoId`, `osId`                             | Mercado Pago confirmou (consultado na API, valor conferido)    |
| `pagamento.recusado`   | `pagamentoId`, `osId`                             | Mercado Pago rejeitou/cancelou                                 |

## Eventos que o Billing CONSOME

O parser aceita o envelope SNS padrão envolvendo o envelope compartilhado (`eventType` + `data`), o envelope legado (`tipo` + `dados`) e JSON direto. O tópico vem do `TopicArn` quando recebido via SNS e valida o envelope interno quando presente. A leitura é case-insensitive.

| Tópico                  | Payload esperado                                                                                          | Ação                                                              |
|-------------------------|-----------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------|
| `diagnostico.concluido` | `{ "osId": guid, "pecas": [{ "pecaId": guid, "quantidade": n }], "servicos": [{ "servicoId": guid, "quantidade": n }] }` | Busca preços no Cadastros (`GET /pecas/{id}`, `/servicos/{id}`) e gera o orçamento |
| `execucao.finalizada`   | `{ "osId": guid }`                                                                                       | Cria a preferência no Mercado Pago e o `Pagamento` (Pendente)     |
| `os.cancelada`          | `{ "osId": guid }`                                                                                       | Compensação: cancela orçamento/cobrança; estorna se já pago       |

O Billing também aceita `ordemServicoId` e itens com `id` como aliases legados, normalizando ambos antes de chamar os handlers.

## Semântica de falha

Handler com `Result.Falha` ou exceção ⇒ mensagem **não** é apagada ⇒ reentrega após 60 s ⇒ DLQ após 5 recebimentos. Falhas do Outbox tentam novamente até `Outbox:MaxTentativas`; ao atingir o limite, o registro permanece no banco com `UltimoErro` e o serviço emite log Critical para intervenção e reprocessamento operacional.

Para localizar eventos que esgotaram as tentativas, consulte `outbox_messages` por `ProcessadoEm IS NULL` e `Tentativas >= Outbox:MaxTentativas`. Após corrigir a causa e conferir o estado do evento no SNS, a operação pode rearmar apenas o registro identificado zerando `Tentativas` e `UltimoErro`; o envio é *at-least-once*, portanto confirme que a publicação anterior não foi aceita antes de reprocessar.
