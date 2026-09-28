# Access token do Mercado Pago (sandbox). Preenchido manualmente no
# console/CLI depois do apply — nunca commitado em texto plano.
resource "aws_secretsmanager_secret" "mercadopago_access_token" {
  name = "oficina/${var.environment}/billing/mercadopago-access-token"
}
