# Database lógico "oficina_billing" dentro do RDS compartilhado (criado
# pelo repositório oficina-infra-db). Cada serviço cria e é dono do seu
# próprio database + usuário — nenhum serviço lê o de outro.

data "aws_ssm_parameter" "rds_endpoint" {
  name = "/oficina/${var.environment}/rds/endpoint"
}

resource "postgresql_database" "billing" {
  name  = "oficina_billing"
  owner = postgresql_role.billing_user.name
}

resource "postgresql_role" "billing_user" {
  name     = "billing_service_user"
  login    = true
  password = random_password.billing_db_password.result
}

resource "random_password" "billing_db_password" {
  length  = 24
  special = false
}

resource "aws_secretsmanager_secret" "billing_db_credentials" {
  name = "oficina/${var.environment}/billing/db-credentials"
}

resource "aws_secretsmanager_secret_version" "billing_db_credentials" {
  secret_id = aws_secretsmanager_secret.billing_db_credentials.id
  secret_string = jsonencode({
    username = postgresql_role.billing_user.name
    password = random_password.billing_db_password.result
    database = postgresql_database.billing.name
  })
}
