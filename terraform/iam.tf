# IRSA: o pod assume esta role via ServiceAccount — nenhuma credencial AWS dentro do container.

locals {
  oidc_host = replace(var.oidc_provider_url, "https://", "")
}

data "aws_caller_identity" "current" {}

resource "aws_iam_role" "billing" {
  name = "oficina-${var.environment}-billing"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Federated = var.oidc_provider_arn }
      Action    = "sts:AssumeRoleWithWebIdentity"
      Condition = {
        StringEquals = {
          "${local.oidc_host}:sub" = "system:serviceaccount:${var.k8s_namespace}:${var.k8s_service_account}"
          "${local.oidc_host}:aud" = "sts.amazonaws.com"
        }
      }
    }]
  })
}

resource "aws_iam_role_policy" "billing" {
  name = "billing-runtime"
  role = aws_iam_role.billing.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "PublishOwnedTopics"
        Effect   = "Allow"
        Action   = ["sns:Publish"]
        Resource = [for t in aws_sns_topic.owned : t.arn]
      },
      {
        Sid    = "ConsumeBillingQueue"
        Effect = "Allow"
        Action = [
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:ChangeMessageVisibility",
          "sqs:GetQueueAttributes",
          "sqs:GetQueueUrl",
        ]
        Resource = [aws_sqs_queue.billing_queue.arn]
      },
      {
        Sid    = "ReadMessagingAndRdsParameters"
        Effect = "Allow"
        Action = ["ssm:GetParameter"]
        Resource = [
          "arn:aws:ssm:${var.aws_region}:${data.aws_caller_identity.current.account_id}:parameter/oficina/${var.environment}/messaging/*",
          "arn:aws:ssm:${var.aws_region}:${data.aws_caller_identity.current.account_id}:parameter/oficina/${var.environment}/rds/endpoint",
        ]
      },
      {
        Sid    = "ReadSecrets"
        Effect = "Allow"
        Action = ["secretsmanager:GetSecretValue"]
        Resource = [
          aws_secretsmanager_secret.mercadopago_access_token.arn,
          aws_secretsmanager_secret.mercadopago_webhook_secret.arn,
          aws_secretsmanager_secret.billing_db_credentials.arn,
        ]
      },
    ]
  })
}
