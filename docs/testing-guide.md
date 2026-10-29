# Guia de testes manuais — Oficina Billing

Este roteiro complementa os testes automatizados e mostra como validar o Billing localmente, inclusive publicando eventos pelo SNS do LocalStack.

> Os comandos deste guia usam **Git Bash**. Não publique eventos de teste em AWS, homologação ou produção; os ARNs e IDs abaixo são para o LocalStack local.

## 1. O que você vai validar

O Billing participa do fluxo de uma ordem de serviço:

1. Consome `diagnostico.concluido`, consulta preços no serviço de Cadastros e cria o orçamento.
2. O cliente aprova ou recusa o orçamento pela API.
3. Consome `execucao.finalizada` e cria uma cobrança no Mercado Pago.
4. Recebe o webhook do Mercado Pago e confirma ou recusa o pagamento.
5. Consome `os.cancelada` para compensar o orçamento e a cobrança.

O Billing publica seus eventos por meio do Outbox. Em produção, esse Outbox publica no SNS. Localmente, há dois modos:

| Modo | Como iniciar | Mensagens de entrada | Eventos de saída |
|---|---|---|---|
| Simples | `make up` | Não há consumidor SNS/SQS; eventos só são logados se o fluxo os produzir | `Messaging:Provider=Log`, aparecem nos logs |
| SNS/SQS local | `make up-aws` | LocalStack encaminha os tópicos de entrada para a fila consumida pelo Billing | Outbox publica no SNS do LocalStack |

Para publicar eventos e testar o fluxo assíncrono, use **`make up-aws`**. Executar apenas `docker compose --profile aws up` não aplica as variáveis que habilitam o publisher AWS e o consumidor.

## 2. Pré-requisitos e inicialização

Instale/inicie Docker Desktop, .NET 10 SDK e GNU Make. Na raiz do projeto:

```bash
cp .env.example .env
make up-aws
docker compose --profile aws ps
```

O `.env` padrão mantém o Mercado Pago fake e não exige credenciais. O alvo `make up-aws` configura o provider AWS local, habilita o consumidor e aponta para `http://localstack:4566`.

Espere os containers ficarem prontos e confirme a API:

```bash
curl -i http://localhost:5001/health
curl -i http://localhost:5001/health/ready
```

Ambos devem responder `200`. A interface Swagger fica em <http://localhost:5001/swagger>.

Para acompanhar processamento e erros em tempo real:

```bash
docker compose logs -f billing-api localstack
```

O serviço `wiremock` simula Cadastros e OS. Qualquer GUID válido de peça/serviço é aceito pelos mappings de Cadastros; no mapping local os preços são R$ 50,00 por peça e R$ 100,00 por serviço.

## 3. Preparar uma fila para observar eventos publicados

A fila `oficina-dev-billing-queue` é a fila de entrada. Ela não recebe os eventos que o Billing publica (`orcamento.*` e `pagamento.*`), pois esses tópicos não têm assinatura para essa fila. Para conferir as publicações, crie uma fila temporária de observação e assine os cinco tópicos de saída:

```bash
TEST_QUEUE_NAME="oficina-dev-billing-events-test-$(date +%s)"

TEST_QUEUE_URL=$(docker compose exec -T localstack awslocal sqs create-queue \
  --queue-name "$TEST_QUEUE_NAME" \
  --query QueueUrl --output text | tr -d '\r')

TEST_QUEUE_ARN=$(docker compose exec -T localstack awslocal sqs get-queue-attributes \
  --queue-url "$TEST_QUEUE_URL" \
  --attribute-names QueueArn \
  --query 'Attributes.QueueArn' --output text | tr -d '\r')

TEST_QUEUE_POLICY=$(printf \
  '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"sns.amazonaws.com"},"Action":"sqs:SendMessage","Resource":"%s"}]}' \
  "$TEST_QUEUE_ARN")

docker compose exec -T localstack awslocal sqs set-queue-attributes \
  --queue-url "$TEST_QUEUE_URL" \
  --attributes "Policy=$TEST_QUEUE_POLICY"

for topico in orcamento-gerado orcamento-aprovado orcamento-recusado pagamento-confirmado pagamento-recusado; do
  docker compose exec -T localstack awslocal sns subscribe \
    --topic-arn "arn:aws:sns:us-east-1:000000000000:oficina-dev-$topico" \
    --protocol sqs \
    --notification-endpoint "$TEST_QUEUE_ARN"
done
```

Guarde `$TEST_QUEUE_URL` neste terminal. Para ler mensagens publicadas:

```bash
docker compose exec -T localstack awslocal sqs receive-message \
  --queue-url "$TEST_QUEUE_URL" \
  --max-number-of-messages 10 \
  --wait-time-seconds 10 \
  --message-attribute-names All
```

O campo `Body` será o envelope SNS. Dentro dele, `Message` contém o envelope compartilhado do Billing (`eventId`, `eventType`, `eventVersion`, `occurredAt`, `correlationId`, `producer` e `data`). O recebimento não apaga a mensagem; ela pode aparecer de novo após o visibility timeout. Essa fila é apenas para observar eventos de saída e não interfere no consumidor do Billing.

Para ver o ARN ou o estado das filas, use:

```bash
docker compose exec -T localstack awslocal sns list-topics
docker compose exec -T localstack awslocal sqs list-queues
docker compose exec -T localstack awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/oficina-dev-billing-queue \
  --attribute-names ApproximateNumberOfMessages ApproximateNumberOfMessagesNotVisible
```

## 4. Fluxo feliz completo com eventos

### 4.1 Publicar `diagnostico.concluido`

Use um GUID novo para a OS em cada execução. O evento envia um item de peça e um serviço; no WireMock, o orçamento esperado é R$ 150,00.

```bash
OS_ID=11111111-1111-4111-8111-111111111111
PECA_ID=22222222-2222-4222-8222-222222222222
SERVICO_ID=33333333-3333-4333-8333-333333333333

DIAGNOSTICO=$(printf \
  '{"eventId":"%s","eventType":"diagnostico.concluido","eventVersion":"1.0","occurredAt":"%s","correlationId":"teste-billing","producer":"execucao-service","data":{"osId":"%s","pecas":[{"pecaId":"%s","quantidade":1}],"servicos":[{"servicoId":"%s","quantidade":1}]}}' \
  "$(uuidgen)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$OS_ID" "$PECA_ID" "$SERVICO_ID")

docker compose exec -T localstack awslocal sns publish \
  --topic-arn arn:aws:sns:us-east-1:000000000000:oficina-dev-diagnostico-concluido \
  --message "$DIAGNOSTICO"
```

O `MessageId` confirma que o SNS aceitou a mensagem, mas não garante por si só que o Billing a processou. Confirme nos logs e consulte a OS:

```bash
docker compose logs --tail=100 billing-api
curl -i "http://localhost:5001/orcamentos/$OS_ID"
```

O `GET` pode retornar `404` enquanto a mensagem ainda está na fila; aguarde alguns segundos e tente novamente. Quando processada, a resposta deve trazer `status: "Gerado"`, `valorTotal: 150` e o `id` do orçamento. O evento `orcamento.gerado` deve aparecer na fila de observação da seção 3.

O contrato completo, inclusive campos opcionais e formatos aceitos, está em [events.md](./events.md).

### 4.2 Aprovar o orçamento

Copie o `id` da resposta anterior:

```bash
ORCAMENTO_ID=<id-retornado-pelo-GET>

curl -i -H 'X-Correlation-Id: teste-billing-123' \
  -X POST "http://localhost:5001/orcamentos/$ORCAMENTO_ID/aprovar"
curl -i "http://localhost:5001/orcamentos/$OS_ID"
```

Esperado: resposta `200`, o mesmo `X-Correlation-Id` na resposta, orçamento com `status: "Aprovado"` e evento `orcamento.aprovado` na fila de observação com `correlationId: "teste-billing-123"`. Enviar a mesma aprovação novamente deve continuar retornando sucesso sem publicar uma segunda aprovação.

### 4.3 Publicar `execucao.finalizada`

Esse evento simula o serviço de Execução terminando o trabalho:

```bash
docker compose exec -T localstack awslocal sns publish \
  --topic-arn arn:aws:sns:us-east-1:000000000000:oficina-dev-execucao-finalizada \
  --message "{\"eventId\":\"$(uuidgen)\",\"eventType\":\"execucao.finalizada\",\"eventVersion\":\"1.0\",\"occurredAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",\"correlationId\":\"teste-billing\",\"producer\":\"execucao-service\",\"data\":{\"osId\":\"$OS_ID\"}}"
```

Confira os logs e consulte o pagamento:

```bash
docker compose logs --tail=100 billing-api
curl -i "http://localhost:5001/pagamentos/$OS_ID"
```

Esperado: pagamento com `status: "Pendente"`, `valor: 150` e `linkPagamento` de teste. Copie o `id` do pagamento da resposta — **não** use o ID da OS nem o `MercadoPagoPaymentId`.

### 4.4 Simular aprovação do pagamento (Mercado Pago fake)

Com `MP_USE_FAKE=true` (padrão), envie o webhook usando `fake-approved-` seguido do ID do pagamento:

```bash
PAGAMENTO_ID=<id-do-pagamento>

curl -i -X POST \
  "http://localhost:5001/pagamentos/webhook/mercado-pago?data.id=fake-approved-$PAGAMENTO_ID&type=payment"
```

Consulte novamente:

```bash
curl -i "http://localhost:5001/pagamentos/$OS_ID"
```

Esperado: `status: "Confirmado"` e evento `pagamento.confirmado` na fila de observação. O fake só reconhece IDs de pagamento que foram criados pelo próprio processo em execução; um ID inventado ou uma reinicialização da API pode resultar em pagamento não encontrado.

## 5. Outros cenários funcionais manuais

Execute os cenários em OSs distintas para evitar conflitos de estado. Para cada OS, comece pela seção 4.1 e use IDs novos.

### Recusar orçamento

Depois de gerar o orçamento, use seu ID:

```bash
curl -i -X POST "http://localhost:5001/orcamentos/$ORCAMENTO_ID/recusar"
curl -i "http://localhost:5001/orcamentos/$OS_ID"
```

Esperado: orçamento `Recusado` e evento `orcamento.recusado`. Não é necessário enviar `execucao.finalizada` nesse cenário.

### Recusar pagamento

Gere e aprove outro orçamento, publique `execucao.finalizada` e obtenha o ID do novo pagamento. Em seguida:

```bash
curl -i -X POST \
  "http://localhost:5001/pagamentos/webhook/mercado-pago?data.id=fake-rejected-$PAGAMENTO_ID&type=payment"
curl -i "http://localhost:5001/pagamentos/$OS_ID"
```

Esperado: pagamento `Recusado` e evento `pagamento.recusado`.

### Cancelar OS com cobrança pendente

Gere e aprove um orçamento, publique `execucao.finalizada` e confirme que o pagamento está `Pendente`. Depois publique a compensação:

```bash
docker compose exec -T localstack awslocal sns publish \
  --topic-arn arn:aws:sns:us-east-1:000000000000:oficina-dev-os-cancelada \
  --message "{\"eventId\":\"$(uuidgen)\",\"eventType\":\"os.cancelada\",\"eventVersion\":\"1.0\",\"occurredAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",\"correlationId\":\"teste-billing\",\"producer\":\"os-service\",\"data\":{\"osId\":\"$OS_ID\",\"motivo\":\"outro\"}}"

curl -i "http://localhost:5001/orcamentos/$OS_ID"
curl -i "http://localhost:5001/pagamentos/$OS_ID"
```

Esperado: orçamento e pagamento `Cancelado`. `os.cancelada` é consumido pelo Billing; não é um evento de saída do Billing.

### Cancelar OS depois de pagamento confirmado

Depois de confirmar o pagamento com `fake-approved-$PAGAMENTO_ID`, publique `os.cancelada` como acima. Esperado: o pagamento muda para `Estornado`; com o gateway fake, o estorno é simulado, sem transferência real de dinheiro.

### Idempotência e correlação

- Publique `diagnostico.concluido` novamente com o mesmo `ordemServicoId`: não deve criar um segundo orçamento.
- Aprove o orçamento duas vezes: não deve gerar eventos `orcamento.aprovado` duplicados.
- Envie o mesmo webhook duas vezes: o estado e os eventos não devem ser duplicados.
- Para testar o cabeçalho de correlação sem alterar o estado, use `curl -i -H 'X-Correlation-Id: teste-billing-123' http://localhost:5001/health`; a resposta deve devolver o mesmo cabeçalho.

## 6. Validar entradas inválidas e respostas HTTP

Use esses casos como smoke tests rápidos:

```bash
# OS inexistente: 404
curl -i http://localhost:5001/orcamentos/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa

# Orçamento inexistente: 404
curl -i -X POST http://localhost:5001/orcamentos/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/aprovar

# Tipo de webhook não relacionado a pagamento: 200, ignorado
curl -i -X POST \
  'http://localhost:5001/pagamentos/webhook/mercado-pago?type=merchant_order&id=teste'

# Tipo payment sem ID: 400
curl -i -X POST \
  'http://localhost:5001/pagamentos/webhook/mercado-pago?type=payment'

# Pagamento inexistente no gateway fake: 200 com processado=false
curl -i -X POST \
  'http://localhost:5001/pagamentos/webhook/mercado-pago?data.id=fake-approved-aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa&type=payment'
```

Se `MercadoPago__ValidateSignature=true`, o endpoint exige uma assinatura `x-signature` válida; para o webhook fake/desenvolvimento, deixe a validação desligada. O teste com assinatura real deve ser feito usando a notificação enviada pelo Mercado Pago Sandbox.

## 7. Testes automatizados

Os testes de integração usam Testcontainers e precisam do Docker funcionando.

```bash
# Solução completa: build e testes unitários, integração e BDD
dotnet test OficinaBilling.sln

# Apenas unitários (não precisam iniciar LocalStack)
dotnet test tests/OficinaBilling.UnitTests/OficinaBilling.UnitTests.csproj

# Apenas integração + BDD (Testcontainers inicia PostgreSQL)
dotnet test tests/OficinaBilling.IntegrationTests/OficinaBilling.IntegrationTests.csproj

# Alvos equivalentes do Makefile
make build
make test
```

Os testes de integração sobem uma API em memória com PostgreSQL real via Testcontainers; substituem Cadastros e o publisher externo por fakes/gravador. Eles **não** publicam no LocalStack nem chamam o Mercado Pago real. A feature BDD em `tests/OficinaBilling.IntegrationTests/Features/Billing.feature` cobre geração, aprovação, recusa, pagamentos confirmados/recusados/duplicados e compensação.

Para gerar cobertura, o projeto disponibiliza `make coverage`; ele executa a suíte e gera `coverage-report/index.html`. O alvo também precisa do `reportgenerator` instalado e configurado no PATH.

## 8. Testar com o Mercado Pago Sandbox real (opcional)

Esse é um teste externo: use apenas credenciais e usuários de teste, nunca credenciais de produção.

1. No painel de desenvolvedores do Mercado Pago, crie uma aplicação Checkout Pro, copie o **Access Token de teste** e crie usuários de teste vendedor/comprador.
2. Configure no `.env`:

   ```dotenv
   MP_USE_FAKE=false
   MP_ACCESS_TOKEN=<access-token-de-teste>
   MP_NOTIFICATION_URL=https://<host-publico>/pagamentos/webhook/mercado-pago
   MP_VALIDATE_SIGNATURE=true
   MP_WEBHOOK_SECRET=<segredo-do-webhook-de-teste>
   ```

3. Exponha a porta local da API com uma URL pública HTTPS (por exemplo, `ngrok http 5001`) e configure a mesma URL no painel de Webhooks, para o evento de pagamentos.
4. Reinicie o stack para aplicar as variáveis e manter o consumidor SNS/SQS ativo:

   ```bash
   make up-aws
   ```

5. Faça as seções 4.1 a 4.3. Abra o `linkPagamento` retornado e pague com o usuário comprador de teste e cartão de teste do painel/documentação do Mercado Pago.
6. Aguarde o webhook assinado do Mercado Pago e consulte `GET /pagamentos/{osId}`. O Billing consulta a API do Mercado Pago antes de aceitar o estado e confere o valor; o evento `pagamento.confirmado` ou `pagamento.recusado` deve chegar à fila de observação.

Não simule manualmente esse webhook com `curl` enquanto a validação de assinatura estiver habilitada; use a notificação legítima do Sandbox.

## 9. Reentrega e DLQ (opcional)

A fila de entrada tem visibility timeout de 60 segundos e move mensagens que falham para `oficina-dev-billing-dlq` após cinco recebimentos. Para observar uma mensagem inválida, publique JSON malformado em um tópico de entrada e acompanhe os logs:

```bash
docker compose exec -T localstack awslocal sns publish \
  --topic-arn arn:aws:sns:us-east-1:000000000000:oficina-dev-diagnostico-concluido \
  --message 'isto-nao-e-json'

docker compose logs -f billing-api
```

Como o intervalo entre tentativas é de 60 segundos, esse teste leva vários minutos. Verifique a DLQ com:

```bash
docker compose exec -T localstack awslocal sqs receive-message \
  --queue-url http://localhost:4566/000000000000/oficina-dev-billing-dlq \
  --max-number-of-messages 10 \
  --wait-time-seconds 10
```

Esse cenário é opcional; não use mensagens inválidas em um ambiente compartilhado.

## 10. Diagnóstico e limpeza

| Sintoma | O que conferir |
|---|---|
| SNS devolveu `MessageId`, mas não há orçamento | Confira `make up-aws`, logs da API, saúde dos containers e se o `TopicArn` é `oficina-dev-diagnostico-concluido`. O `MessageId` só confirma que SNS aceitou a publicação. |
| API encerra durante a inicialização | A API agora repete a busca da fila enquanto o LocalStack inicializa e os containers têm política `unless-stopped`. Confira `docker compose --profile aws ps -a` e `docker compose logs --tail=100 billing-api localstack`. |
| Logs mostram coluna do Outbox inexistente | O volume PostgreSQL pode ter sido criado por uma versão antiga do serviço. Faça backup antes de qualquer migração ou limpeza; não use `down -v` se precisar preservar esses dados. |
| `GET /orcamentos/{osId}` retorna `404` logo após publicar | O consumo é assíncrono; aguarde e consulte de novo. Se persistir, leia os logs e a quantidade de mensagens visíveis/não visíveis na fila. |
| `execucao.finalizada` não cria pagamento | O evento precisa apontar para uma OS que tenha orçamento aprovado. Confira `ordemServicoId` e os logs do consumidor. |
| `fake-approved-...` não confirma | Use o `id` retornado em `GET /pagamentos/{osId}` e confirme que o gateway fake criou a cobrança nesse mesmo processo da API. |
| Evento de saída não aparece | Confirme que a fila de observação foi criada e assinada antes de executar o fluxo; confira também os logs do Outbox e a fila de observação. |
| Docker/Testcontainers falha nos testes | Confirme Docker Desktop ativo e rode novamente `dotnet test tests/OficinaBilling.IntegrationTests/OficinaBilling.IntegrationTests.csproj`. |

Para parar os serviços e manter os dados locais:

```bash
make down
```

Para começar do zero, removendo também o banco e os dados dos volumes locais:

```bash
docker compose --profile aws down -v
```

> `down -v` apaga os dados persistidos do banco local. Não execute se quiser preservar orçamentos/pagamentos usados em outros testes.
