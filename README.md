# oficina-billing-service

Serviço de **Orçamentos + Pagamentos** da Oficina Mecânica (Fase 4 — microsserviços).
Dono: Tavares (Papel B). Participante do Saga orquestrado pelo **OS Service**.

Alinhado aos contratos oficiais do pacote da Fase 4: `contratos/openapi/billing.yaml`,
`contratos/eventos/schemas.md` e `planos/plano-tavares-billing-service.md`.

## Estrutura (Clean Architecture)

```
src/
├── OficinaBilling.Domain/          # Entidades, regras de negócio, eventos. Não depende de nada.
├── OficinaBilling.Application/     # Use cases + "portas" (interfaces) para o mundo externo
├── OficinaBilling.Infrastructure/  # EF Core, HttpClient p/ vizinhos, SNS/SQS, Mercado Pago
└── OficinaBilling.API/             # Controllers, Program.cs, injeção de dependência
```

Regra de dependência: `API → Application → Domain`, com `Infrastructure` implementando
as interfaces que `Application` declara.

## Status atual

`Domain` e `Application` foram **compilados com sucesso** (`dotnet build`, .NET 10, 0 erros)
no ambiente onde este scaffold foi gerado. `Infrastructure` e `API` usam pacotes NuGet
(EF Core, AWS SDK, SDK do Mercado Pago) que não pude restaurar nesse ambiente — então
não foram compilados de fato aqui. Rode `dotnet restore` na sua máquina antes de continuar;
é o primeiro passo do checklist abaixo.

| Camada | O que tem | Status |
|---|---|---|
| **Domain** | `Orcamento` (com validade/expiração/cancelamento por compensação), `Pagamento`, `OrcamentoItem`, `OutboxMessage`, `EventoProcessado` | ✅ completo, compilado |
| **Application** | Todos os use cases implementados: `GerarOrcamento`, `AprovarOrcamento`, `RecusarOrcamento`, `ConsultarOrcamento`, `CancelarOrcamentoPorOs`, `CriarCobranca`, `ConsultarPagamento`, `ProcessarWebhookMercadoPago` | ✅ completo, compilado |
| **Infrastructure/Persistence** | `BillingDbContext`, EF Configurations, Repositories, `UnitOfWork`, `OutboxRepository`, `IdempotenciaRepository` | ✅ completo, não compilado aqui |
| **Infrastructure/ExternalServices** | `OsServiceClient`, `CadastrosServiceClient` (REST), `MercadoPagoGateway` (SDK real `mercadopago-sdk` v2.4.1) | ✅ completo, não compilado aqui |
| **Infrastructure/Messaging** | `OutboxEventPublisher`, `OutboxPublisherBackgroundService` (outbox → SNS), `BillingQueueConsumer` (1 consumer, despacha por `eventType`) | ✅ completo, não compilado aqui |
| **API** | `Program.cs` com toda a DI, Controllers batendo 1:1 com `billing.yaml` | ✅ completo, não compilado aqui |
| **Testes** | 3 testes unitários de `GerarOrcamentoUseCase` (falha peça inexistente, soma correta, idempotência) | ✅ escritos, não rodados aqui |
| **EF Core Migration inicial** | — | ❌ falta gerar (`dotnet ef migrations add InitialCreate`) |
| **BDD (SpecFlow/Reqnroll)** | — | ❌ não iniciado (plano: semana 5) |

## Fluxo de ponta a ponta implementado

1. `diagnostico.concluido` chega na fila → `BillingQueueConsumer` → `GerarOrcamentoUseCase`
   busca preço de cada peça/serviço no Cadastros → soma → salva `Orcamento` + grava evento
   `orcamento.gerado` na Outbox (mesma transação)
2. `OutboxPublisherBackgroundService` lê a Outbox a cada 5s e publica no SNS
3. Cliente chama `POST /orcamentos/{id}/aprovar` → `orcamento.aprovado` na Outbox
4. `execucao.finalizada` chega na fila → `CriarCobrancaUseCase` cria a preferência no
   Mercado Pago e salva `Pagamento` como `Pendente`
5. Mercado Pago chama `POST /pagamentos/webhook/mercado-pago` → confirma o status real
   na API do MP (nunca confia só no payload) → `pagamento.confirmado`/`recusado` na Outbox
6. `os.cancelada` chega na fila → `CancelarOrcamentoPorOsUseCase` cancela o orçamento
   pendente (compensação da saga, não publica evento novo)

## Como testar localmente (sem AWS)

A mensageria (SNS/SQS) fica **desligada por padrão** em `appsettings.Development.json`
(`Messaging:Enabled = false`), então dá pra testar o REST sozinho:

```bash
# 1. Restaurar pacotes (precisa de acesso à internet — não rodei isso aqui)
dotnet restore

# 2. Subir só o Postgres
docker compose up -d db

# 3. Gerar e aplicar a migration inicial (ainda não existe neste scaffold)
dotnet ef migrations add InitialCreate --project src/OficinaBilling.Infrastructure --startup-project src/OficinaBilling.API
dotnet ef database update --project src/OficinaBilling.Infrastructure --startup-project src/OficinaBilling.API

# 4. Rodar a API
dotnet run --project src/OficinaBilling.API
```

Com o serviço no ar:
- `GET /health` → deve responder 200
- Para testar `GerarOrcamentoUseCase` sem esperar o consumer SQS: chame o use case a
  partir de um teste, ou monte um endpoint de teste temporário — o fluxo "de produção"
  é sempre via evento, não REST direto
- `Services:OsService:BaseUrl` e `Services:CadastrosService:BaseUrl` apontam pra
  `localhost:5100`/`5200` em dev — suba wiremocks nessas portas com os contratos de
  `contratos/openapi/os.yaml` e `cadastros.yaml` (paliativo padronizado do grupo,
  ver `fase4-pacote/README.md`) enquanto os serviços reais do Rafael/Pedro não sobem

Para testar a mensageria de verdade sem esperar a AWS Academy, considere subir um
LocalStack no `docker-compose.yml` (não incluído neste scaffold) e apontar
`Messaging:QueueUrl`/`AWS` pra ele.

## Como se conecta aos outros serviços

**REST síncrono (você chama):** `OS Service` (`GET /ordens-servico/{id}`), `Cadastros Service`
(`GET /pecas/{id}`, `GET /servicos/{id}`).

**Você publica (dono):** `orcamento.gerado`, `orcamento.aprovado`, `orcamento.recusado`,
`pagamento.confirmado`, `pagamento.recusado`.

**Você consome (não é dono):** `diagnostico.concluido`, `execucao.finalizada`, `os.cancelada`
— todos numa fila única `oficina-{env}-billing-queue`, despachados por `eventType` dentro
do próprio `BillingQueueConsumer`.

> Regra do grupo: cada serviço só cria/edita os SNS topics que publica, no próprio
> Terraform (`terraform/messaging.tf`). Para consumir topic de outro serviço, lê o ARN
> via Parameter Store (`terraform/subscriptions.tf`) — nunca edita o repositório alheio.

## Próximos passos (ordem sugerida)

1. `dotnet restore` + `dotnet build` na sua máquina, corrigir o que o compilador reclamar
   (não consegui validar Infrastructure/API neste ambiente sem acesso ao NuGet)
2. `dotnet ef migrations add InitialCreate` + `dotnet ef database update`
3. Subir wiremocks de OS/Cadastros local e testar o fluxo REST (aprovar/recusar/consultar)
4. Preencher `NotificationUrl` no `MercadoPagoGateway` com a URL pública do webhook (ngrok
   em dev local — ver seção "cuidados" do plano)
5. Escrever os 2 ADRs globais (Saga Orquestrado vs Coreografado; SNS/SQS vs alternativas)
6. `terraform init` + `apply` (dev) e testar mensageria de verdade (ou via LocalStack local)
7. Cenários BDD (SpecFlow/Reqnroll) — semana 5 do plano

## Rodando com Docker Compose

```bash
make up      # sobe Postgres + API
make test    # roda os testes (precisa dotnet restore antes)
make down    # derruba tudo
```
