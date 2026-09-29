# Assinaturas de topics que o Billing CONSOME, mas não é dono:
#   - diagnostico.concluido (Execução Service) -> gera orçamento
#   - execucao.finalizada   (Execução Service) -> cria cobrança no Mercado Pago
#   - os.cancelada          (OS Service)       -> cancela orçamento pendente (compensação)
#
# O ARN de cada topic é lido via Parameter Store (publicado pelo Terraform do
# serviço dono) — nunca hardcoded, nunca editando o repositório do outro serviço.

data "aws_ssm_parameter" "diagnostico_concluido_arn" {
  name = "/oficina/${var.environment}/messaging/topics/diagnostico.concluido/arn"
}

data "aws_ssm_parameter" "execucao_finalizada_arn" {
  name = "/oficina/${var.environment}/messaging/topics/execucao.finalizada/arn"
}

data "aws_ssm_parameter" "os_cancelada_arn" {
  name = "/oficina/${var.environment}/messaging/topics/os.cancelada/arn"
}

resource "aws_sqs_queue" "billing_dlq" {
  name = "oficina-${var.environment}-billing-dlq"
}

resource "aws_sqs_queue" "billing_queue" {
  name = "oficina-${var.environment}-billing-queue"

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.billing_dlq.arn
    maxReceiveCount      = 5
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
          "aws:SourceArn" = [
            data.aws_ssm_parameter.diagnostico_concluido_arn.value,
            data.aws_ssm_parameter.execucao_finalizada_arn.value,
            data.aws_ssm_parameter.os_cancelada_arn.value,
          ]
        }
      }
    }]
  })
}

# raw_message_delivery = true: a mensagem que cai na fila é o envelope puro do
# evento (sem o wrapper de notificação do SNS) — é isso que o BillingQueueConsumer espera.
resource "aws_sns_topic_subscription" "diagnostico_concluido" {
  topic_arn             = data.aws_ssm_parameter.diagnostico_concluido_arn.value
  protocol               = "sqs"
  endpoint                = aws_sqs_queue.billing_queue.arn
  raw_message_delivery = true
}

resource "aws_sns_topic_subscription" "execucao_finalizada" {
  topic_arn             = data.aws_ssm_parameter.execucao_finalizada_arn.value
  protocol               = "sqs"
  endpoint                = aws_sqs_queue.billing_queue.arn
  raw_message_delivery = true
}

resource "aws_sns_topic_subscription" "os_cancelada" {
  topic_arn             = data.aws_ssm_parameter.os_cancelada_arn.value
  protocol               = "sqs"
  endpoint                = aws_sqs_queue.billing_queue.arn
  raw_message_delivery = true
}
