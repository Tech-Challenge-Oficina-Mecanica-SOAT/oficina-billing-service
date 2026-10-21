#!/usr/bin/env bash
# Lê o que o Billing PUBLICOU no SNS (via fila de debug assinando os 5 tópicos de saída).
#   ./kit/scripts/read-published.sh          # mostra (sem apagar)
#   ./kit/scripts/read-published.sh --limpar # mostra e esvazia a fila
source "$(dirname "$0")/_lib.sh"
out=$(awslocal sqs receive-message --queue-url "$(queue_url $DEBUG_QUEUE_NAME)" --max-number-of-messages 10 \
  --wait-time-seconds 2 --visibility-timeout 0 --output json)
echo "$out" | jq -r '.Messages[]? | .Body | fromjson | "\(.occurredAt)  \(.eventType)  \(.data | tojson)"' | sort
[[ "${1:-}" == "--limpar" ]] && awslocal sqs purge-queue --queue-url "$(queue_url $DEBUG_QUEUE_NAME)" >/dev/null && info "fila de debug esvaziada"
exit 0
