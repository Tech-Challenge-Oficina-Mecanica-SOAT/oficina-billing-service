#!/bin/bash
# Roda dentro do LocalStack quando ele fica pronto. Espelha, em miniatura, o terraform/ do projeto:
#   - 1 tópico SNS por tipo de evento (8)
#   - parâmetros SSM /oficina/dev/messaging/topics/<eventType>/arn  (é onde o TopicArnResolver procura)
#   - fila oficina-dev-billing-queue (+ DLQ com redrive após 5 tentativas) assinando os 3 tópicos que o Billing CONSOME
#   - fila oficina-dev-debug-queue assinando os 5 tópicos que o Billing PUBLICA, só para você ler o que saiu
set -euo pipefail

ENV=dev
CONSUMIDOS=(diagnostico.concluido execucao.finalizada os.cancelada)
PUBLICADOS=(orcamento.gerado orcamento.aprovado orcamento.recusado pagamento.confirmado pagamento.recusado)

echo "[kit] criando filas..."
DLQ_URL=$(awslocal sqs create-queue --queue-name "oficina-$ENV-billing-dlq" --query QueueUrl --output text)
DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url "$DLQ_URL" --attribute-names QueueArn --query Attributes.QueueArn --output text)

cat > /tmp/billing-queue-attrs.json <<JSON
{"RedrivePolicy":"{\"deadLetterTargetArn\":\"$DLQ_ARN\",\"maxReceiveCount\":\"5\"}"}
JSON
QUEUE_URL=$(awslocal sqs create-queue --queue-name "oficina-$ENV-billing-queue" --attributes file:///tmp/billing-queue-attrs.json --query QueueUrl --output text)
QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url "$QUEUE_URL" --attribute-names QueueArn --query Attributes.QueueArn --output text)

DEBUG_URL=$(awslocal sqs create-queue --queue-name "oficina-$ENV-debug-queue" --query QueueUrl --output text)
DEBUG_ARN=$(awslocal sqs get-queue-attributes --queue-url "$DEBUG_URL" --attribute-names QueueArn --query Attributes.QueueArn --output text)

criar_topico() {
  local tipo="$1" nome
  nome="oficina-$ENV-${tipo//./-}"
  local arn
  arn=$(awslocal sns create-topic --name "$nome" --query TopicArn --output text)
  awslocal ssm put-parameter --name "/oficina/$ENV/messaging/topics/$tipo/arn" --type String --value "$arn" --overwrite >/dev/null
  echo "$arn"
}

echo "[kit] criando tópicos + parâmetros SSM + assinaturas..."
for tipo in "${CONSUMIDOS[@]}"; do
  arn=$(criar_topico "$tipo")
  awslocal sns subscribe --topic-arn "$arn" --protocol sqs --notification-endpoint "$QUEUE_ARN" \
    --attributes RawMessageDelivery=true >/dev/null
done
for tipo in "${PUBLICADOS[@]}"; do
  arn=$(criar_topico "$tipo")
  awslocal sns subscribe --topic-arn "$arn" --protocol sqs --notification-endpoint "$DEBUG_ARN" \
    --attributes RawMessageDelivery=true >/dev/null
done

echo "[kit] pronto: billing-queue, billing-dlq, debug-queue, 8 tópicos, 8 parâmetros SSM."
