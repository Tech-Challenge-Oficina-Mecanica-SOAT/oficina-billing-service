#!/usr/bin/env bash
# Funções comuns aos scripts do kit. Requer: docker compose, curl, jq.
API="${API:-http://localhost:5001}"
QUEUE_NAME="oficina-dev-billing-queue"
DEBUG_QUEUE_NAME="oficina-dev-debug-queue"
DLQ_NAME="oficina-dev-billing-dlq"

# IDs fixos dos mocks do WireMock (kit/wiremock/mappings/cadastros.json)
PECA_PASTILHA="11111111-1111-1111-1111-111111111111"   # R$ 150,00
PECA_FILTRO="22222222-2222-2222-2222-222222222222"     # R$  45,90
SERV_OLEO="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"       # R$ 200,00
SERV_ALINHAMENTO="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" # R$ 120,00

cd "$(dirname "${BASH_SOURCE[0]}")/../.." # raiz do projeto (onde está o docker-compose.yml)

for cmd in docker curl jq; do
  command -v "$cmd" >/dev/null || { echo "Faltando '$cmd' no PATH."; exit 1; }
done

BOLD=$'\033[1m'; DIM=$'\033[2m'; GREEN=$'\033[32m'; RED=$'\033[31m'; YELLOW=$'\033[33m'; RESET=$'\033[0m'
passo()  { echo; echo "${BOLD}▶ $*${RESET}"; }
ok()     { echo "  ${GREEN}✔${RESET} $*"; }
erro()   { echo "  ${RED}✘ $*${RESET}"; }
info()   { echo "  ${DIM}$*${RESET}"; }
aviso()  { echo "  ${YELLOW}! $*${RESET}"; }

uuid() {
  if command -v uuidgen >/dev/null; then uuidgen | tr 'A-Z' 'a-z'
  elif [[ -r /proc/sys/kernel/random/uuid ]]; then cat /proc/sys/kernel/random/uuid
  else python3 -c 'import uuid; print(uuid.uuid4())'; fi
}

awslocal() { docker compose exec -T localstack awslocal "$@"; }

queue_url() { echo "http://localhost:4566/000000000000/$1"; } # visto de DENTRO do container do LocalStack

# Monta o envelope padrão (contratos/eventos/schemas.md) e envia para a fila de entrada do Billing.
# uso: enviar_evento <eventType> '<json do data>'
enviar_evento() {
  local tipo="$1" data="$2" id envelope
  id=$(uuid)
  envelope=$(jq -nc --arg t "$tipo" --argjson d "$data" --arg id "$id" --arg now "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    '{eventId:$id, eventType:$t, eventVersion:"1.0", occurredAt:$now, correlationId:$id, producer:"kit-de-teste", data:$d}')
  awslocal sqs send-message --queue-url "$(queue_url $QUEUE_NAME)" --message-body "$envelope" >/dev/null
  info "enviado $tipo (eventId $id)"
}

# Espera um GET devolver 200 e imprime o corpo. uso: esperar_200 <url> [segundos]
esperar_200() {
  local url="$1" limite="${2:-40}" i body code
  for ((i = 0; i < limite; i++)); do
    body=$(curl -s -o /tmp/kit-resp.json -w '%{http_code}' "$url") && code="$body"
    if [[ "$code" == "200" ]]; then cat /tmp/kit-resp.json; return 0; fi
    sleep 1
  done
  return 1
}

# Espera um campo JSON de um GET ter determinado valor. uso: esperar_campo <url> <jq-path> <valor> [segundos]
esperar_campo() {
  local url="$1" campo="$2" esperado="$3" limite="${4:-40}" i atual
  for ((i = 0; i < limite; i++)); do
    atual=$(curl -s "$url" | jq -r "$campo // empty" 2>/dev/null || true)
    [[ "$atual" == "$esperado" ]] && return 0
    sleep 1
  done
  return 1
}

verificar_api() {
  curl -sf "$API/health" >/dev/null || { erro "API não responde em $API/health. Rode: docker compose up -d --build"; exit 1; }
}

# Webhook "sem assinatura" (use com MP_VALIDATE_SIGNATURE=false, o padrão do compose)
chamar_webhook() {
  local pid="$1"
  curl -s -o /dev/null -w '%{http_code}' -X POST "$API/pagamentos/webhook/mercado-pago?data.id=$pid&type=payment"
}

body_diagnostico() { # <osId> — 2x pastilha + 1 troca de óleo = R$ 1,00
  jq -nc --arg os "$1" --arg d "$(uuid)" --arg p "$PECA_PASTILHA" --arg s "$SERV_OLEO" \
    '{osId:$os, diagnosticoId:$d, pecas:[{pecaId:$p, quantidade:2}], servicos:[{servicoId:$s, quantidade:1}]}'
}
