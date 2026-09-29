# Topics dos quais o Billing é DONO — ninguém mais edita isto.
# Cada topic também publica seu ARN no Parameter Store, para que outros
# serviços possam assinar sem precisar editar este repositório.

locals {
  owned_topics = [
    "orcamento.gerado",
    "orcamento.aprovado",
    "orcamento.recusado",
    "pagamento.confirmado",
    "pagamento.recusado",
  ]
}

resource "aws_sns_topic" "owned" {
  for_each = toset(local.owned_topics)
  name     = "oficina-${var.environment}-${replace(each.value, ".", "-")}"
}

resource "aws_ssm_parameter" "owned_topic_arn" {
  for_each = aws_sns_topic.owned
  name     = "/oficina/${var.environment}/messaging/topics/${each.key}/arn"
  type     = "String"
  value    = each.value.arn
}
