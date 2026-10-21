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

`Domain` e `Application` compilam (`dotnet build`, .NET 10). `Infrastructure` e `API` usam pacotes
NuGet (EF Core, AWS SDK, SDK do Mercado Pago) que **não puderam ser restaurados** no ambiente onde
as últimas correções foram feitas; a lógica que não depende desses pacotes (use cases, controller do
webhook, dispatcher de eventos, publisher da Outbox, assinatura) foi compilada e **executada** num
harness com repositórios em memória (60 verificações, incluindo HTTP real). O que depende de EF/AWS/SDK
do MP só foi revisado, não executado: rode `dotnet restore && dotnet build` na sua máquina primeiro.
Veja `docs/GUIA-DE-TESTES.md` para o que mudou e como testar.

| Camada | O que tem | Status |
|---|---|---|
| **Domain** | `Orcamento` (com validade/expiração/cancelamento por compensação), `Pagamento`, `OrcamentoItem`, `OutboxMessage`, `EventoProcessado` | ✅ completo, compilado |
| **Application** | Todos os use cases implementados: `GerarOrcamento`, `AprovarOrcamento`, `RecusarOrcamento`, `ConsultarOrcamento`, `CancelarOrcamentoPorOs`, `CriarCobranca`, `ConsultarPagamento`, `ProcessarWebhookMercadoPago` | ✅ completo, compilado |
| **Infrastructure/Persistence** | `BillingDbContext`, EF Configurations, Repositories, `UnitOfWork`, `OutboxRepository`, `IdempotenciaRepository` | ✅ completo, não compilado aqui |
| **Infrastructure/ExternalServices** | `OsServiceClient`, `CadastrosServiceClient` (REST), `MercadoPagoGateway` (SDK real `mercadopago-sdk` v2.4.1) | ✅ completo, não compilado aqui |
| **Infrastructure/Messaging** | `OutboxEventPublisher`, `OutboxPublisherBackgroundService` (outbox → SNS), `BillingQueueConsumer` (1 consumer, despacha por `eventType`) | ✅ completo, não compilado aqui |
| **API** | `Program.cs` com toda a DI, Controllers batendo 1:1 com `billing.yaml` | ✅ completo, não compilado aqui |
| **Testes** | `GerarOrcamento` (3, com Moq) + webhook, cobrança, aprovação expirada, `Pagamento`, `Orcamento` e assinatura do webhook (fakes em memória) | ✅ escritos; os novos foram executados num harness, `dotnet test` não rodou aqui |
| **Kit de teste local** | `docker-compose` com Postgres + LocalStack (SNS/SQS/SSM) + WireMock, gateway MP fake, endpoints `/dev/*`, scripts de cenário em `kit/scripts/` | ✅ novo (ver `docs/GUIA-DE-TESTES.md`) |
| **EF Core Migration inicial** | — | ❌ falta gerar (`dotnet ef migrations add InitialCreate`). Em dev, `Database:EnsureCreated=true` cria as tabelas sem migration |
| **BDD (SpecFlow/Reqnroll)** | — | ❌ não iniciado (plano: semana 5) |

## Fluxo de ponta a ponta implementado

1. `diagnostico.concluido` chega na fila → `BillingQueueConsumer` → `GerarOrcamentoUseCase`
   busca preço de cada peça/serviço no Cadastros → soma → salva `Orcamento` + grava evento
   `orcamento.gerado` na Outbox (mesma transação)
2. `OutboxPublisherBackgroundService` lê a Outbox a cada 5s e publica no SNS
3. Cliente chama `POST /orcamentos/{id}/aprovar` → `orcamento.aprovado` na Outbox
4. `execucao.finalizada` chega na fila → `CriarCobrancaUseCase` cria a preferência no
   Mercado Pago e salva `Pagamento` como `Pendente`
5. Mercado Pago chama `POST /pagamentos/webhook/mercado-pago` → valida `x-signature` → confirma o
   status real na API do MP (nunca confia só no payload) → localiza o `Pagamento` pelo
   `external_reference` (= Id do Orçamento) → `pagamento.confirmado`/`recusado` na Outbox
6. `os.cancelada` chega na fila → `CancelarOrcamentoPorOsUseCase` cancela o orçamento
   pendente (compensação da saga, não publica evento novo)

## Como testar localmente

Guia completo em **[docs/GUIA-DE-TESTES.md](docs/GUIA-DE-TESTES.md)**. Resumo:

```bash
docker compose up -d --build        # Postgres + LocalStack + WireMock + API (MP fake, /dev/* ligado)
./kit/scripts/fluxo-feliz.sh        # diagnóstico -> orçamento -> aprova -> cobrança -> webhook -> confirmado
./kit/scripts/fluxo-pagamento-recusado.sh
./kit/scripts/fluxo-cancelamento.sh
./kit/scripts/read-published.sh     # o que o Billing publicou no SNS
```

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

1. `dotnet restore` + `dotnet build` + `dotnet test` na sua máquina; corrigir o que o compilador reclamar
   em Infrastructure/API (não foi possível compilar essas camadas com os pacotes reais)
2. `docker compose up -d --build` e rodar os scripts de `kit/scripts/`
3. `dotnet ef migrations add InitialCreate` (e desligar `Database:EnsureCreated` fora de dev)
4. Testar com o **Mercado Pago sandbox real** (ngrok + `MercadoPago:NotificationUrl` + `WebhookSecret`),
   passo a passo no guia
5. Escrever os 2 ADRs globais (Saga Orquestrado vs Coreografado; SNS/SQS vs alternativas)
6. `terraform init` + `apply` (dev)
7. Cenários BDD (SpecFlow/Reqnroll) — semana 5 do plano

## Comandos úteis

```bash
make deps    # sobe só Postgres + LocalStack + WireMock (rode a API pelo IDE / dotnet run)
make up      # sobe tudo, incluindo a API em container
make flow    # roda o fluxo feliz
make logs    # logs da API
make down    # derruba tudo (e apaga os volumes)
make test    # dotnet test
```
