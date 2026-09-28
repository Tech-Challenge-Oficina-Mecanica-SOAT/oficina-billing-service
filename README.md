# oficina-billing-service

Serviço de **Orçamentos + Pagamentos** da Oficina Mecânica (Fase 4 — microsserviços).
Dono: Tavares. Participante no Saga orquestrado pelo **OS Service**.

## Por que essa estrutura

Segue a mesma **Clean Architecture** do `oficina-mecanica` (Fase 3), pra reaproveitar o
que o grupo já domina — só que agora tudo dentro de um único serviço pequeno, dono do
próprio banco, do próprio Terraform e do próprio deploy.

```
src/
├── OficinaBilling.Domain/          # Entidades, regras de negócio, eventos. Não depende de nada.
├── OficinaBilling.Application/     # Use cases + "portas" (interfaces) para o mundo externo
├── OficinaBilling.Infrastructure/  # EF Core, HttpClient p/ vizinhos, SNS/SQS, Mercado Pago
└── OficinaBilling.API/             # Controllers, Program.cs, injeção de dependência
```

Regra de dependência: `API → Application → Domain`, com `Infrastructure` implementando
as interfaces que `Application` declara. O `Domain` nunca sabe que Postgres, SNS ou
Mercado Pago existem.

## O que já está esboçado

| Pasta | Status |
|---|---|
| `Domain/Entities` (`Orcamento`, `Pagamento`) | ✅ completo, com regras de transição de status |
| `Application/UseCases/Orcamento/GerarOrcamento` | ✅ completo — fluxo principal (consulta Cadastros, soma, persiste, publica evento) |
| `Application/UseCases/Pagamento/ProcessarWebhookMercadoPago` | ✅ completo — idempotente |
| Demais use cases (`Aprovar`, `Recusar`, `Consultar...`, `CriarCobranca`) | 🚧 esqueleto, `TODO` marcado |
| `Infrastructure/ExternalServices` (clientes REST + Mercado Pago) | 🚧 REST clients prontos; `MercadoPagoGateway` ainda lança `NotImplementedException` |
| `Infrastructure/Messaging` (SNS publisher + consumers SQS) | 🚧 publisher pronto; consumers são só o esqueleto (precisam virar `BackgroundService`) |
| `terraform/` | ✅ estrutura completa: database lógico, topics que este serviço **é dono**, assinaturas dos topics que **consome** |
| `k8s/`, `Dockerfile`, `docker-compose.yml`, `.github/workflows/ci.yml` | ✅ prontos para ajustar |

## Como este serviço se conecta aos outros

**Síncrono (REST, você chama):**
- `OS Service` → busca dados da ordem de serviço
- `Cadastros Service` → busca preço de peça/serviço

**Assíncrono (você é dono — publica):**
`orcamento.gerado`, `orcamento.aprovado`, `orcamento.recusado`, `pagamento.confirmado`, `pagamento.recusado`

**Assíncrono (você consome — não é dono):**
`diagnostico.concluido` (do Execução Service) dispara `GerarOrcamentoUseCase`
`execucao.finalizada` (do Execução Service) dispara `CriarCobrancaUseCase`

> Regra de ouro do grupo: cada serviço só cria/edita os SNS topics que publica, no
> próprio Terraform. Para consumir o topic de outro serviço, você lê o ARN dele via
> Parameter Store (`terraform/subscriptions.tf`) — nunca edita o repositório alheio.

## Rodando local

```bash
make up      # sobe Postgres + API via docker compose
make test    # roda os testes
make down    # derruba tudo
```

## Próximos passos sugeridos (nessa ordem)

1. `dotnet restore` no repo pra confirmar que os csproj resolvem (ajuste as
   versões de pacote no `Infrastructure.csproj`, foram postas de forma ilustrativa)
2. Implementar `EF Core Configurations` (`Persistence/Configurations`) e a primeira migration
3. Implementar `MercadoPagoGateway` de verdade com o SDK oficial (sandbox)
4. Transformar `DiagnosticoConcluidoConsumer` e `ExecucaoFinalizadaConsumer` em `BackgroundService`
5. `terraform init` + `apply` (dev) — cria o database lógico, os topics e a queue
6. Preencher os use cases marcados como `TODO`
7. Escrever os 2 ADRs globais (Saga Orquestrado vs Coreografado; SNS/SQS vs alternativas)

## Testes

- `tests/OficinaBilling.UnitTests` — já tem 1 teste de exemplo do `GerarOrcamentoUseCase` com Moq + FluentAssertions
- `tests/OficinaBilling.IntegrationTests` — esqueleto com Testcontainers.PostgreSql, ainda vazio
