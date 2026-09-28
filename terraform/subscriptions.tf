# Assinaturas de topics que o Billing CONSOME, mas não é dono.
# Lê o ARN via Parameter Store (publicado pelo Terraform do serviço dono) —
# nunca hardcoded, nunca editando o repositório do outro serviço.

data "aws_ssm_parameter" "diagnostico_concluido_arn" {
  name = "/oficina/${var.environment}/messaging/topics/diagnostico.concluido/arn"
}

data "aws_ssm_parameter" "execucao_finalizada_arn" {
  name = "/oficina/${var.environment}/messaging/topics/execucao.finalizada/arn"
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
          ]
        }
      }
    }]
  })
}

resource "aws_sns_topic_subscription" "diagnostico_concluido" {
  topic_arn = data.aws_ssm_parameter.diagnostico_concluido_arn.value
  protocol  = "sqs"
  endpoint  = aws_sqs_queue.billing_queue.arn
}

resource "aws_sns_topic_subscription" "execucao_finalizada" {
  topic_arn = data.aws_ssm_parameter.execucao_finalizada_arn.value
  protocol  = "sqs"
  endpoint  = aws_sqs_queue.billing_queue.arn
}
