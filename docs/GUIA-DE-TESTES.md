# Guia de testes do Billing Service

Este guia tem três partes: **o que foi corrigido**, **como rodar o kit local** (sem AWS e sem Mercado Pago real) e **como testar com o sandbox do Mercado Pago de verdade**.

> **Honestidade sobre o que foi validado.** `Domain` e `Application` compilam. A lógica que não depende de EF Core / AWS SDK / SDK do Mercado Pago (use cases, controller do webhook, dispatcher de eventos, publisher da Outbox, assinatura) foi compilada e executada num harness com repositórios em memória e Kestrel real: 60 verificações, todas OK. **Não foi possível restaurar o NuGet**, então `Infrastructure` (EF, SQS/SNS/SSM, `MercadoPagoGateway`) e `Program.cs` foram revisados, mas **não compilados nem executados**. O primeiro passo na sua máquina é `dotnet restore && dotnet build && dotnet test`.

---

## 1. O que foi corrigido

| # | Problema | Correção | Onde |
|---|---|---|---|
| 1 | O webhook nunca achava o pagamento: comparava o *payment id* com o `PreferenceId` | Busca por `external_reference` (= Id do Orçamento, já enviado ao criar a preferência) | `ProcessarWebhookMercadoPagoUseCase`, `IPagamentoRepository.ObterPorOrcamentoIdAsync`, `StatusPagamentoMp.ExternalReference` |
| 2 | Controller exigia `X-Idempotency-Key` e `?id=`, que o MP não envia (400 automático, MP reenvia) | Aceita `?data.id=`, `?id=&topic=` (IPN legado) e `data.id` no corpo (número ou string). Idempotência passou a ser `mp:{paymentId}:{status}` | `WebhooksController` |
| 3 | Nenhuma validação de assinatura | Valida `x-signature` (HMAC-SHA256) quando `MercadoPago:ValidateSignature=true`. Sem segredo configurado, rejeita | `MercadoPagoWebhookSignature`, `WebhooksController` |
| 4 | `NotificationUrl = null`: o MP não tinha para onde notificar | Configurável em `MercadoPago:NotificationUrl` | `MercadoPagoGateway`, `MercadoPagoOptions` |
| 5 | JSON da Outbox em PascalCase; o consumer lê `eventType`/`osId` (camelCase) e o contrato é camelCase | `JsonSerializerDefaults.Web` | `OutboxEventPublisher` |
| 6 | Falha de negócio no consumer era ignorada: mensagem apagada, evento perdido, nunca chegava na DLQ | Dispatcher **lança exceção** em `Result.Falha`; a mensagem não é apagada, é reentregue e vai para a DLQ após 5 tentativas | `BillingEventDispatcher`, `BillingQueueConsumer` |
| 7 | `CriarCobranca` não checava se o orçamento estava `Aprovado` | Checa | `CriarCobrancaUseCase` |
| 8 | Orçamento vencido marcado `Expirado` em memória, nunca salvo | Salva o status antes de devolver a falha | `AprovarOrcamentoUseCase` |
| 9 | Valor pago não era comparado ao valor cobrado | `approved` com valor diferente **não** confirma | `ProcessarWebhookMercadoPagoUseCase` |
| 10 | Primeira recusa era definitiva, mas o Checkout Pro deixa tentar outro cartão na mesma preferência | `Pagamento.Confirmar` aceita `Recusado → Confirmado` (ver nota abaixo) | `Pagamento` |
| 11 | `AddOpenApi()` sem o pacote | `Microsoft.AspNetCore.OpenApi` 10.0.0 no csproj da API | `OficinaBilling.API.csproj` |
| 12 | Compose subia a API com a config de produção (URLs do k8s, token vazio) e sem como apontar para LocalStack | `AWS:ServiceURL`, `Database:EnsureCreated`, compose em Development | `Program.cs`, `docker-compose.yml` |

**Nota sobre o item 10 (decisão de negócio).** Se o cliente for recusado e depois aprovado, o serviço publica `pagamento.recusado` e depois `pagamento.confirmado` para a mesma OS. O OS Service precisa tolerar isso (ou compensar). A alternativa, ignorar a aprovação, deixaria o cliente cobrado com a OS marcada como recusada. Para reverter, volte a condição de `Confirmar` para `Status != Pendente → false` e remova o teste `Aprovacao_depois_de_recusa_confirma...`.

---

## 2. Kit local

### Pré-requisitos
Docker + Docker Compose, `curl`, `jq`. (`openssl` só para `webhook-assinado.sh`.)

### Subir
```bash
docker compose up -d --build
docker compose logs -f billing-api     # espere "Now listening on: http://[::]:8080"
curl localhost:5001/health
```

O que sobe:

| Serviço | Porta no host | Papel |
|---|---|---|
| `db` | 5433 | Postgres. As tabelas são criadas por `Database:EnsureCreated=true` (atalho de dev, sem migrations) |
| `localstack` | 4566 | SNS + SQS + SSM. `kit/localstack/init/ready.d/01-setup.sh` cria 8 tópicos, `billing-queue` (+ DLQ, 5 tentativas), os 8 parâmetros SSM e a `debug-queue` |
| `wiremock` | 5100 | Faz de Cadastros e OS Service (`kit/wiremock/mappings/`) |
| `billing-api` | 5001 | API em Development, `MercadoPago:UseFake=true`, endpoints `/dev/*` |

> LocalStack está fixado na `3.8.1`. Versões mais novas podem exigir conta/token. Se o init não rodar, confirme que `01-setup.sh` está executável (`chmod +x`).

### Dados dos mocks
| Id | Item | Preço |
|---|---|---|
| `11111111-1111-1111-1111-111111111111` | Pastilha de freio | 150,00 |
| `22222222-2222-2222-2222-222222222222` | Filtro de óleo | 45,90 |
| `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` | Troca de óleo | 200,00 |
| `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb` | Alinhamento | 120,00 |

Qualquer outro id devolve 404 no WireMock, e o Billing trata como "não encontrado".

### Cenários (scripts em `kit/scripts/`)

| Script | O que prova |
|---|---|
| `fluxo-feliz.sh` | Saga completa: evento → orçamento (R$ 1) → aprovação → cobrança → "pagamento" → webhook → Confirmado → **exatamente 1** `pagamento.confirmado` publicado, mesmo com o webhook repetido |
| `fluxo-pagamento-recusado.sh` | `rejected` → Recusado; nova tentativa `approved` → Confirmado; os dois eventos saem |
| `fluxo-cancelamento.sh` | `os.cancelada` cancela orçamento `Gerado` e **não** mexe em `Aprovado` |
| `fluxo-dlq.sh` | Falha de negócio → 5 reentregas → DLQ (leva ~3 min) |
| `webhook-assinado.sh <paymentId>` | 401 sem assinatura, 401 com assinatura errada, 200 com a certa. Suba antes com `MP_VALIDATE_SIGNATURE=true docker compose up -d billing-api` |
| `send-event.sh <tipo> '<json>'` | Injeta qualquer evento na fila de entrada |
| `read-published.sh [--limpar]` | Lê o que o Billing publicou no SNS (fila de debug) |

Para o dia a dia há também `kit/requests.http` (VS Code REST Client) com todas as chamadas.

### Como o fluxo funciona, e onde olhar

1. **Entrada.** `send-event.sh` coloca um envelope na `billing-queue`. `BillingQueueConsumer` faz long polling e entrega o corpo ao `BillingEventDispatcher`, que decide pelo `eventType`.
2. **Idempotência de infraestrutura.** O dispatcher consulta `IIdempotenciaRepository` pelo `eventId`. Visto = duplicata, ignora.
3. **Use case.** `GerarOrcamentoUseCase` busca os preços no Cadastros (WireMock), soma, salva e **enfileira o evento na Outbox na mesma transação**. Não chama o SNS direto.
4. **Publicação.** `OutboxPublisherBackgroundService` varre a Outbox a cada ~5 s e publica no SNS. Daí a espera de ~6 s nos scripts. Veja o estado em `GET /dev/outbox` (`publicadoEm`, `tentativas`, `ultimoErro`).
5. **Cobrança.** `CriarCobrancaUseCase` cria a preferência com `external_reference = orcamento.Id` e guarda `Pagamento` como `Pendente` com o link.
6. **Webhook.** O MP avisa que *algo* mudou em um `payment id`. O Billing **não confia no corpo**: consulta o payment no MP, obtém status, valor e `external_reference`, acha o `Pagamento` por ele e aplica a transição.
7. **Duas barreiras de idempotência no webhook:** a chave `mp:{id}:{status}` e a própria regra do domínio (`Confirmar`/`Recusar` devolvem `false` se nada muda).

Se quiser ver cada passo acontecendo, deixe aberto `docker compose logs -f billing-api` num terminal e `watch -n2 'curl -s localhost:5001/dev/outbox | jq -c ".[] | {eventType, publicadoEm, tentativas}"'` em outro.

### Modo híbrido (API no IDE, dependências em container)
```bash
docker compose up -d db localstack wiremock        # ou: make deps
Messaging__Enabled=true dotnet run --project src/OficinaBilling.API
```
`appsettings.Development.json` já aponta para `localhost:5433/4566/5100`, liga o MP fake e os endpoints `/dev/*`. Com isso você pode depurar com breakpoints em qualquer use case.

### Inspecionando por fora
```bash
docker compose exec db psql -U billing_user -d oficina_billing -c '\dt'
docker compose exec db psql -U billing_user -d oficina_billing -c 'select "OsId", "Status", valor, "MercadoPagoId", "MotivoRecusa" from pagamentos;'
docker compose exec db psql -U billing_user -d oficina_billing -c 'select "OsId", "Status", valor_total from orcamentos;'
docker compose exec db psql -U billing_user -d oficina_billing -c 'select * from eventos_processados;'   # chaves de idempotência (eventId e mp:<id>:<status>)
docker compose exec localstack awslocal sqs list-queues
docker compose exec localstack awslocal ssm get-parameters-by-path --path /oficina/dev/messaging/topics --recursive
```

### Testes unitários
```bash
dotnet test
```
Novos: `PagamentoTests`, `OrcamentoTests`, `ProcessarWebhookMercadoPagoUseCaseTests`, `CriarCobrancaUseCaseTests`, `AprovarOrcamentoUseCaseTests`, `MercadoPagoWebhookSignatureTests`. Usam fakes em memória (`tests/.../Fakes/Fakes.cs`) em vez de Moq, porque os cenários precisam de estado entre chamadas (o mesmo webhook duas vezes, recusa seguida de aprovação). Cada bug corrigido na seção 1 tem um teste de regressão.

---

## 3. Mercado Pago sandbox de verdade

O kit usa, por padrão, um MP fake. Para validar a integração real (SDK, preferência, checkout, webhook de verdade), faça isto **depois** de o kit estar verde. Os outros serviços (Execução e OS) continuam **simulados pelo kit**: quem faz o papel deles é o `send-event.sh`, que publica `diagnostico.concluido` e `execucao.finalizada` na fila. Só o pagamento passa a ser real. A interface do painel do MP muda com frequência; confira a documentação deles se algum nome divergir.

**Preparação (uma vez)**

1. **Credenciais de teste.** No painel de desenvolvedores do Mercado Pago, crie uma aplicação e copie o *Access Token de teste* (`TEST-...`).
2. **Usuários de teste.** Crie um usuário *vendedor* e um *comprador* de teste (seção "Contas de teste"). No checkout, pague com o **comprador**; a aplicação deve ser do vendedor.
3. **URL pública.** `ngrok http 5001` e anote `https://xxxx.ngrok-free.app`.
4. **Configure o `.env`.** `cp .env.example .env` e preencha `MP_USE_FAKE=false`, `MP_ACCESS_TOKEN` e `MP_NOTIFICATION_URL` (a URL do ngrok + `/pagamentos/webhook/mercado-pago?source_news=webhooks`). Deixe `MP_VALIDATE_SIGNATURE=false` na primeira rodada.
5. **Recrie a API:** `docker compose up -d --force-recreate billing-api`.

**Rodar**

```bash
./kit/scripts/fluxo-mp-real.sh
```

O script simula a Execução, aprova o orçamento, dispara `execucao.finalizada`, imprime o **link real do Checkout Pro** e fica esperando o webhook. Você abre o link, paga com o usuário comprador de teste e o script mostra o status mudando para `Confirmado`. Ele recusa rodar se a API ainda estiver com o MP fake.

No cartão de teste, o resultado costuma ser escolhido pelo **nome do titular** (por exemplo `APRO` aprova e `OTHE` recusa); a lista completa está na documentação de cartões de teste. Para ver recusa seguida de nova tentativa, pague primeiro com um nome que recusa e depois, no mesmo link, com um que aprova.

**Ligando a validação de assinatura (segunda rodada)**

1. No painel de *Webhooks*, cadastre a URL do ngrok, marque o evento **Pagamentos** e copie a **assinatura secreta**.
2. No `.env`: `MP_VALIDATE_SIGNATURE=true` e `MP_WEBHOOK_SECRET=<a assinatura secreta>`; recrie a API e rode o script de novo.
3. Se com `false` funciona e com `true` o ngrok mostra 401, o suspeito é o segredo ou o formato da assinatura. Não tenho como confirmar aqui que as notificações enviadas por `notification_url` são assinadas com o mesmo segredo do painel; é exatamente isso que esse teste vai responder.

**Observando**

`docker compose logs -f billing-api` e o painel do ngrok (`http://localhost:4040`), onde você vê o POST do MP e a resposta. O botão de **simular notificação** do painel manda um `data.id` fictício; como esse payment não existe, o Billing registra "não encontrado" e responde 200. Para um teste de verdade, use um pagamento real.

> `MercadoPago__UseFake` só é aceito em `Development`; em qualquer outro ambiente a API recusa subir. Para voltar ao fake, ponha `MP_USE_FAKE=true` (ou apague o `.env`) e recrie a API.

---

## 4. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Log `Cannot load library libgssapi_krb5.so.2` | Inofensivo: o driver do Postgres procura a biblioteca de Kerberos, que a imagem do .NET não traz. Não impede nada |
| Container da API sai com código 0 logo após subir; log com `NullReferenceException` no `BillingQueueConsumer` | Era o AWS SDK v4 devolvendo `Messages = null` quando o long polling vem vazio. Corrigido; reconstrua com `docker compose up -d --build billing-api` |
| `QueueDoesNotExistException` no log | O init do LocalStack ainda não tinha terminado. O compose agora espera o healthcheck do LocalStack. Para conferir: `docker compose exec localstack awslocal sqs list-queues` (devem aparecer `billing-queue`, `billing-dlq`, `debug-queue`). Se estiver vazio, veja `docker compose logs localstack \| grep kit` |
| Init do LocalStack não roda no Windows | Scripts `.sh` com final de linha CRLF quebram. O `.gitattributes` do projeto força LF; se você extraiu/clonou antes, rode `git add --renormalize .` ou converta `kit/**/*.sh` para LF |
| Logs cheios de `SELECT ... FROM outbox_messages` | É o worker da Outbox (a cada 5 s). Em Development o EF já está em `Warning`; para ver o SQL de volta, troque `Microsoft.EntityFrameworkCore.Database.Command` para `Information` em `appsettings.Development.json` |
| Orçamento nunca aparece | `localstack` ainda iniciando (o consumer tenta de novo a cada 5 s); confira `docker compose logs localstack` e se `01-setup.sh` rodou |
| `/dev/*` retorna 404 | API não está em Development ou `Dev__EnableTestEndpoints` não é `true` |
| Webhook responde 401 | `ValidateSignature=true` sem assinatura válida, ou `WebhookSecret` vazio |
| Webhook 200 mas o pagamento não muda | Veja o log: `sem external_reference válido`, `sem Pagamento correspondente` ou `Valor pago diferente` |
| Webhook 500 | A API do MP falhou ou `data.id` não é numérico (modo real). É proposital: o MP reenvia |
| Eventos não saem no `read-published.sh` | Outbox ainda não varrida (~5 s) ou SSM sem os parâmetros; veja `/dev/outbox` → `ultimoErro` |
| Primeiro `docker compose up` falha ao baixar imagem | Rede/Docker Hub; o LocalStack está em `3.8.1` de propósito |

---

## 5. Limitações conhecidas (não corrigidas)

- **Outbox com várias réplicas.** O k8s sobe 2 a 6 réplicas e todas varrem a mesma tabela sem `FOR UPDATE SKIP LOCKED`; pode haver publicação duplicada (consumidores já são idempotentes pelo `eventId`). Corrigir exige SQL específico do Postgres e validação com banco real.
- **`refunded` / `charged_back`** do MP não são tratados (o webhook só ignora).
- **Retenção de `eventos_processados`:** a tabela só cresce.
- **Migrations:** ainda não existe a inicial. `EnsureCreated` é só para dev; ele não evolui o schema.
- **`CadastrosServiceClient` trata qualquer status não-2xx como "não encontrado"**, inclusive 5xx. Com a correção do consumer isso vira retry e depois DLQ, o que é aceitável, mas a mensagem de erro engana.
- **Infrastructure/API não foram compiladas aqui** (sem NuGet). Pontos de maior risco de compilação: assinaturas dos clientes AWS com `ServiceURL`/`BasicAWSCredentials` em `Program.cs`, e `pagamento.ExternalReference` do SDK do MP.
