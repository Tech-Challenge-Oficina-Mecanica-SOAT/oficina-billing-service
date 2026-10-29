#!/usr/bin/env bash
# Demonstra a validação do x-signature. Suba a API com a validação LIGADA:
#   MP_VALIDATE_SIGNATURE=true docker compose up -d billing-api
# uso: ./kit/scripts/webhook-assinado.sh <paymentId>     (crie o payment antes com POST /dev/mp/pagar)
source "$(dirname "$0")/_lib.sh"
PID="${1:?uso: $0 <paymentId>}"
SECRET="${MP_SECRET:-segredo-de-teste-local}"
TS=$(date +%s); RID=$(uuid)
MANIFESTO="id:${PID};request-id:${RID};ts:${TS};"
SIG=$(printf '%s' "$MANIFESTO" | openssl dgst -sha256 -hmac "$SECRET" -hex | awk '{print $NF}')
URL="$API/pagamentos/webhook/mercado-pago?data.id=$PID&type=payment"

info "texto assinado: $MANIFESTO"
passo "1) sem assinatura (esperado 401 com a validação ligada)"
curl -s -o /dev/null -w "  HTTP %{http_code}\n" -X POST "$URL"
passo "2) assinatura errada (esperado 401)"
curl -s -o /dev/null -w "  HTTP %{http_code}\n" -X POST "$URL" -H "x-request-id: $RID" -H "x-signature: ts=$TS,v1=deadbeef"
passo "3) assinatura correta (esperado 200)"
curl -s -o /dev/null -w "  HTTP %{http_code}\n" -X POST "$URL" -H "x-request-id: $RID" -H "x-signature: ts=$TS,v1=$SIG"
