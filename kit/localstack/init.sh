#!/bin/bash
# Cria no LocalStack o mesmo desenho do Terraform: topics (próprios e de terceiros), fila + DLQ,
# assinaturas e os parâmetros SSM com os ARNs. Roda sozinho quando o LocalStack fica "ready".
set -euo pipefail
export AWS_DEFAULT_REGION=us-east-1
ENV=dev
ACCOUNT=000000000000

for t in orcamento.gerado orcamento.aprovado orcamento.recusado pagamento.confirmado pagamento.recusado \
         diagnostico.concluido execucao.finalizada os.cancelada; do
  arn=$(awslocal sns create-topic --name "oficina-${ENV}-${t//./-}" --query TopicArn --output text)
  awslocal ssm put-parameter --name "/oficina/${ENV}/messaging/topics/${t}/arn" --type String --value "$arn" --overwrite >/dev/null
done

awslocal sqs create-queue --queue-name "oficina-${ENV}-billing-dlq" >/dev/null
DLQ_ARN="arn:aws:sqs:us-east-1:${ACCOUNT}:oficina-${ENV}-billing-dlq"
awslocal sqs create-queue --queue-name "oficina-${ENV}-billing-queue" \
  --attributes "{\"VisibilityTimeout\":\"60\",\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"${DLQ_ARN}\\\",\\\"maxReceiveCount\\\":\\\"5\\\"}\"}" >/dev/null
QUEUE_ARN="arn:aws:sqs:us-east-1:${ACCOUNT}:oficina-${ENV}-billing-queue"

for t in diagnostico.concluido execucao.finalizada os.cancelada; do
  awslocal sns subscribe --topic-arn "arn:aws:sns:us-east-1:${ACCOUNT}:oficina-${ENV}-${t//./-}" \
    --protocol sqs --notification-endpoint "$QUEUE_ARN" >/dev/null
done

echo "LocalStack do Billing pronto (topics, fila, DLQ e assinaturas)."
