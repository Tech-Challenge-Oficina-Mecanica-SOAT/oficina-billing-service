# Assinaturas de topics que o Billing CONSOME, mas não é dono.
# Lê o ARN via Parameter Store (publicado pelo Terraform do serviço dono) —
# nunca hardcoded, nunca editando o repositório do outro serviço.

locals {
  consumed_topics = [
    "diagnostico.concluido", # Execução -> gera o orçamento
    "execucao.finalizada",   # Execução -> cria a cobrança no Mercado Pago
    "os.cancelada",          # OS (orquestrador do Saga) -> compensação
  ]
}

data "aws_ssm_parameter" "consumed_topic_arn" {
  for_each = toset(local.consumed_topics)
  name     = "/oficina/${var.environment}/messaging/topics/${each.value}/arn"
}

resource "aws_sqs_queue" "billing_dlq" {
  name                      = "oficina-${var.environment}-billing-dlq"
  message_retention_seconds = 1209600 # 14 dias para investigar
}

resource "aws_sqs_queue" "billing_queue" {
  name                       = "oficina-${var.environment}-billing-queue"
  visibility_timeout_seconds = 60 # > tempo máximo de processamento (chamadas REST + banco)
  receive_wait_time_seconds  = 20 # long polling

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.billing_dlq.arn
    maxReceiveCount     = 5
<<<<<<< Updated upstream
  })
}

resource "aws_sqs_queue_redrive_allow_policy" "billing_dlq" {
  queue_url = aws_sqs_queue.billing_dlq.id
  redrive_allow_policy = jsonencode({
    redrivePermission = "byQueue"
    sourceQueueArns   = [aws_sqs_queue.billing_queue.arn]
=======
>>>>>>> Stashed changes
  })
}

resource "aws_sqs_queue_policy" "billing_queue_policy" {
  queue_url = aws_sqs_queue.billing_queue.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "sns.amazonaws.com" }
      Action    = "sqs:SendMessage"
      Resource  = aws_sqs_queue.billing_queue.arn
      Condition = {
        ArnEquals = {
          "aws:SourceArn" = [for p in data.aws_ssm_parameter.consumed_topic_arn : p.value]
        }
      }
    }]
  })
}

<<<<<<< Updated upstream
# Sem raw_message_delivery: o consumidor lê o TopicArn do envelope SNS para rotear a mensagem.
resource "aws_sns_topic_subscription" "consumed" {
  for_each  = data.aws_ssm_parameter.consumed_topic_arn
  topic_arn = each.value.value
  protocol  = "sqs"
  endpoint  = aws_sqs_queue.billing_queue.arn
=======
# raw_message_delivery = true: a mensagem que cai na fila é o envelope puro do
# evento (sem o wrapper de notificação do SNS) — é isso que o BillingQueueConsumer espera.
resource "aws_sns_topic_subscription" "diagnostico_concluido" {
  topic_arn            = data.aws_ssm_parameter.diagnostico_concluido_arn.value
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.billing_queue.arn
  raw_message_delivery = true
}

resource "aws_sns_topic_subscription" "execucao_finalizada" {
  topic_arn            = data.aws_ssm_parameter.execucao_finalizada_arn.value
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.billing_queue.arn
  raw_message_delivery = true
}

resource "aws_sns_topic_subscription" "os_cancelada" {
  topic_arn            = data.aws_ssm_parameter.os_cancelada_arn.value
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.billing_queue.arn
  raw_message_delivery = true
>>>>>>> Stashed changes
}
