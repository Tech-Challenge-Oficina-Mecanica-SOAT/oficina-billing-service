#!/usr/bin/env bash
# Caminho feliz completo, ponta a ponta:
#   diagnostico.concluido -> orçamento -> aprovação (REST) -> execucao.finalizada -> cobrança no MP (fake)
#   -> cliente "paga" -> webhook -> pagamento Confirmado -> pagamento.confirmado publicado
source "$(dirname "$0")/_lib.sh"
verificar_api
OS=$(uuid)

passo "1) Evento diagnostico.concluido (OS $OS): 2x pastilha (150) + 1 troca de óleo (200) = R\$ 1"
enviar_evento diagnostico.concluido "$(body_diagnostico "$OS")"
ORC=$(esperar_200 "$API/orcamentos/$OS") || { erro "orçamento não apareceu (veja: docker compose logs billing-api)"; exit 1; }
echo "$ORC" | jq -c '{id, status, valorTotal}'
ORC_ID=$(echo "$ORC" | jq -r .id)
# comparação numérica: o jq 1.7 imprime 1.00 (não "1") quando o JSON traz casas decimais
echo "$ORC" | jq -e '.valorTotal == 1' >/dev/null && ok "valor total 1" || erro "valor total inesperado"

passo "2) Cliente aprova o orçamento (REST)"
code=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/orcamentos/$ORC_ID/aprovar")
[[ "$code" == "204" ]] && ok "HTTP 204" || erro "HTTP $code"

passo "3) Evento execucao.finalizada -> Billing cria a cobrança (preferência no MP fake)"
enviar_evento execucao.finalizada "$(jq -nc --arg os "$OS" '{osId:$os}')"
esperar_200 "$API/pagamentos/$OS" >/dev/null || { erro "cobrança não apareceu"; exit 1; }
curl -s "$API/pagamentos/$OS" | jq -c '{status, valor, linkPagamento}'

passo "4) Cliente 'paga' no checkout (simulado) e o MP chama o webhook"
PID=$(curl -s -X POST "$API/dev/mp/pagar" -H 'Content-Type: application/json' -d "$(jq -nc --arg os "$OS" '{osId:$os,status:"approved"}')" | jq -r .paymentId)
info "payment id (fake): $PID"
code=$(chamar_webhook "$PID"); [[ "$code" == "200" ]] && ok "webhook -> HTTP 200" || erro "webhook -> HTTP $code"
esperar_campo "$API/pagamentos/$OS" .status Confirmado 10 && ok "pagamento Confirmado" || erro "pagamento NÃO ficou Confirmado"

passo "5) O mesmo webhook de novo (MP reenvia!) — não pode duplicar nada"
code=$(chamar_webhook "$PID"); info "webhook repetido -> HTTP $code"

passo "6) Eventos que o Billing publicou no SNS (espere ~6s pelo worker da Outbox)"
sleep 6
./kit/scripts/read-published.sh | grep -F "$OS" || true
n=$(./kit/scripts/read-published.sh | grep -F "$OS" | grep -c pagamento.confirmado || true)
[[ "$n" == "1" ]] && ok "exatamente 1 pagamento.confirmado" || erro "esperado 1 pagamento.confirmado, achei $n"
