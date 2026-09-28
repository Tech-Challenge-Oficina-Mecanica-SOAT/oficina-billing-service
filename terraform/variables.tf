variable "environment" {
  description = "Ambiente de deploy (ex: dev, prod)"
  type        = string
  default     = "dev"
}

variable "aws_region" {
  type    = string
  default = "us-east-1"
}
