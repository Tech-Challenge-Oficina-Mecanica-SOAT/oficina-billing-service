#!/usr/bin/env bash
# Falha de negócio -> a mensagem NÃO é apagada -> SQS reentrega (5x) -> cai na DLQ.
# Leva ~3 minutos (VisibilityTimeout de 30s x 5 tentativas). Antes da correção, o evento era descartado em silêncio.
source "$(dirname "$0")/_lib.sh"
verificar_api
OS=$(uuid)

passo "Enviando execucao.finalizada para uma OS que NÃO tem orçamento (OS $OS)"
enviar_evento execucao.finalizada "$(jq -nc --arg os "$OS" '{osId:$os}')"
info "acompanhe em outro terminal:  docker compose logs -f billing-api"

passo "Aguardando as 5 tentativas (até 4 min)..."
for i in $(seq 1 48); do
  n=$(awslocal sqs get-queue-attributes --queue-url "$(queue_url $DLQ_NAME)" --attribute-names ApproximateNumberOfMessages \
        --query Attributes.ApproximateNumberOfMessages --output text 2>/dev/null || echo 0)
  [[ "$n" -ge 1 ]] && { ok "mensagem chegou na DLQ ($n)"; break; }
  sleep 5
done
[[ "${n:-0}" -ge 1 ]] || aviso "ainda não chegou na DLQ; olhe os logs"
passo "Corpo da mensagem na DLQ"
awslocal sqs receive-message --queue-url "$(queue_url $DLQ_NAME)" --visibility-timeout 0 --output json \
  | jq -r '.Messages[0].Body // "(vazia)"' | jq -c . 2>/dev/null || true
