variable "environment" {
  description = "Ambiente de deploy (dev, homolog, prod)"
  type        = string
  default     = "dev"
}

variable "aws_region" {
  type    = string
  default = "us-east-1"
}

variable "rds_admin_username" {
  description = "Usuário administrador do RDS compartilhado (só para criar database/role)."
  type        = string
  sensitive   = true
}

variable "rds_admin_password" {
  description = "Senha do administrador do RDS compartilhado."
  type        = string
  sensitive   = true
}

variable "oidc_provider_arn" {
  description = "ARN do OIDC provider do cluster EKS (IRSA)."
  type        = string
}

variable "oidc_provider_url" {
  description = "URL do OIDC provider do cluster EKS, ex.: https://oidc.eks.us-east-1.amazonaws.com/id/XXXX"
  type        = string
}

variable "k8s_namespace" {
  description = "Namespace do Billing no cluster."
  type        = string
  default     = "oficina"
}

variable "k8s_service_account" {
  description = "ServiceAccount usado pelo pod (deve bater com k8s/base/serviceaccount.yaml)."
  type        = string
  default     = "oficina-billing-service"
}
