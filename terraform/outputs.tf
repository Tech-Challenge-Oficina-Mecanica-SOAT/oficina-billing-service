output "billing_queue_url" {
  value = aws_sqs_queue.billing_queue.id
}

output "billing_dlq_url" {
  value = aws_sqs_queue.billing_dlq.id
}

output "billing_db_secret_arn" {
  value = aws_secretsmanager_secret.billing_db_credentials.arn
}

output "billing_role_arn" {
  description = "Colocar na annotation eks.amazonaws.com/role-arn do ServiceAccount (overlay k8s do ambiente)."
  value       = aws_iam_role.billing.arn
}
