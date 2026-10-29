#!/usr/bin/env bash
# Compensação da saga: os.cancelada cancela o orçamento ainda pendente; não mexe em orçamento aprovado.
source "$(dirname "$0")/_lib.sh"
verificar_api

OS_A=$(uuid); OS_B=$(uuid)

passo "Orçamento A (fica 'Gerado') e B (será aprovado)"
enviar_evento diagnostico.concluido "$(body_diagnostico "$OS_A")"
enviar_evento diagnostico.concluido "$(body_diagnostico "$OS_B")"
esperar_200 "$API/orcamentos/$OS_A" >/dev/null; ORC_B=$(esperar_200 "$API/orcamentos/$OS_B" | jq -r .id)
curl -s -o /dev/null -X POST "$API/orcamentos/$ORC_B/aprovar"
ok "A=Gerado, B=Aprovado"

passo "os.cancelada para A e para B"
enviar_evento os.cancelada "$(jq -nc --arg os "$OS_A" '{osId:$os}')"
enviar_evento os.cancelada "$(jq -nc --arg os "$OS_B" '{osId:$os}')"
esperar_campo "$API/orcamentos/$OS_A" .status Cancelado 15 && ok "A virou Cancelado" || erro "A deveria estar Cancelado"
sleep 2
st=$(curl -s "$API/orcamentos/$OS_B" | jq -r .status)
[[ "$st" == "Aprovado" ]] && ok "B continua Aprovado (compensação não desfaz orçamento já decidido)" || erro "B está '$st'"
