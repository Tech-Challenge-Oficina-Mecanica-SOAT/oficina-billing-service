# Preenchidos manualmente (console/CLI) depois do apply — nunca commitados em texto plano.
# O pod lê ambos no startup via Secrets Manager (ver SecretsLoader e a role IRSA em iam.tf).
resource "aws_secretsmanager_secret" "mercadopago_access_token" {
  name        = "oficina/${var.environment}/billing/mercadopago-access-token"
  description = "Access Token do Mercado Pago (credencial de TESTE em dev/homolog)."
}

resource "aws_secretsmanager_secret" "mercadopago_webhook_secret" {
  name        = "oficina/${var.environment}/billing/mercadopago-webhook-secret"
  description = "Chave secreta do webhook (painel do Mercado Pago > Webhooks), usada para validar x-signature."
}
