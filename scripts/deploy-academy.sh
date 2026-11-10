#!/usr/bin/env bash
# Deploy do Billing no AWS Academy (sem IRSA/OIDC/ALB). Ver README, secao "Deploy no AWS Academy".
# Uso: IMAGE_TAG=<sha> ./scripts/deploy-academy.sh
# Requer: credenciais da sessao exportadas (AWS_ACCESS_KEY_ID/SECRET/SESSION_TOKEN), kubectl apontando para o EKS.
# As credenciais expiram em cerca de 4 horas: reexecute o script com as novas credenciais.
set -euo pipefail
export AWS_DEFAULT_REGION=${AWS_DEFAULT_REGION:-us-east-1} MSYS_NO_PATHCONV=1
IMAGE_TAG=${IMAGE_TAG:-365c070e3b24}   # tag sha da imagem no GHCR; use um sha que inclua a correcao do contrato do Cadastros
ENV=homolog; NS=oficina

ssm() { aws ssm get-parameter --name "$1" --query Parameter.Value --output text; }
DB_ENDPOINT=$(ssm /oficina/$ENV/db/endpoint); DB_PORT=$(ssm /oficina/$ENV/db/port); DB_USER=$(ssm /oficina/$ENV/db/username)
DB_PASSWORD=$(aws secretsmanager get-secret-value --secret-id oficina/$ENV/db-password --query SecretString --output text)

# Secret com a connection string (RDS admin compartilhado) e as credenciais da sessao (substituem o IRSA).
kubectl create secret generic billing-academy-secrets -n $NS \
  --from-literal=ConnectionStrings__BillingDb="Host=$DB_ENDPOINT;Port=$DB_PORT;Database=oficina_billing;Username=$DB_USER;Password=$DB_PASSWORD;Trust Server Certificate=true" \
  --from-literal=AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:?}" \
  --from-literal=AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:?}" \
  --from-literal=AWS_SESSION_TOKEN="${AWS_SESSION_TOKEN:?}" \
  --dry-run=client -o yaml | kubectl apply -f - >/dev/null
echo "Secret billing-academy-secrets aplicado."

# Licenca do New Relic: reaproveita a do Secret compartilhado oficina-secrets, se existir.
NR=$(kubectl get secret oficina-secrets -n $NS -o jsonpath='{.data.NEW_RELIC_LICENSE_KEY}' 2>/dev/null | base64 -d || true)
if [ -n "$NR" ]; then
  kubectl create secret generic newrelic-license -n $NS --from-literal=license-key="$NR" --dry-run=client -o yaml | kubectl apply -f - >/dev/null
  echo "Secret newrelic-license aplicado."
fi

cd "$(dirname "${BASH_SOURCE[0]}")/.."
kubectl kustomize k8s/overlays/academy | sed "s#:IMAGE_TAG_PLACEHOLDER#:${IMAGE_TAG}#" | kubectl apply -f -
kubectl rollout restart deployment/oficina-billing-service -n $NS
kubectl rollout status deployment/oficina-billing-service -n $NS --timeout=5m
echo "Billing no ar (interno): http://oficina-billing-service.$NS.svc.cluster.local"
