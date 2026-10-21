#!/usr/bin/env bash
# Envia um evento qualquer para a fila de entrada do Billing (como se viesse do SNS).
#   ./kit/scripts/send-event.sh execucao.finalizada '{"osId":"<uuid>"}'
source "$(dirname "$0")/_lib.sh"
[[ $# -eq 2 ]] || { echo "uso: $0 <eventType> '<json do data>'"; exit 1; }
enviar_evento "$1" "$2"
