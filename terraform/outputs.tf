output "billing_queue_url" {
  value = aws_sqs_queue.billing_queue.id
}

output "billing_db_secret_arn" {
  value = aws_secretsmanager_secret.billing_db_credentials.arn
}
