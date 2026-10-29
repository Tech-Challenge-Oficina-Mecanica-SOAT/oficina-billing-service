terraform {
  required_version = ">= 1.6"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    postgresql = {
      source  = "cyrilgdn/postgresql"
      version = "~> 1.22"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # State remoto por ambiente. Bucket/chave entram via `terraform init -backend-config=...` (ver CI):
  #   -backend-config="bucket=<bucket>" -backend-config="key=billing/<env>.tfstate" -backend-config="region=<regiao>"
  backend "s3" {}
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "oficina"
      Service     = "billing"
      Environment = var.environment
      ManagedBy   = "terraform"
    }
  }
}

# O RDS é compartilhado e criado pelo repositório oficina-infra-db; aqui só criamos o
# database lógico e o role do Billing. A conexão administrativa vem de variáveis sensíveis (TF_VAR_*).
provider "postgresql" {
  host            = data.aws_ssm_parameter.rds_endpoint.value
  port            = 5432
  username        = var.rds_admin_username
  password        = var.rds_admin_password
  sslmode         = "require"
  superuser       = false
  connect_timeout = 15
}
