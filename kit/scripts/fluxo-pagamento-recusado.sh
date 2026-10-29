#!/usr/bin/env bash
# Pagamento recusado e depois uma NOVA TENTATIVA aprovada na mesma preferência (Checkout Pro permite).
source "$(dirname "$0")/_lib.sh"
verificar_api
OS=$(uuid)

passo "Preparando OS $OS (diagnóstico -> aprova -> execução finalizada)"
enviar_evento diagnostico.concluido "$(body_diagnostico "$OS")"
ORC_ID=$(esperar_200 "$API/orcamentos/$OS" | jq -r .id) || { erro "orçamento não apareceu"; exit 1; }
curl -s -o /dev/null -X POST "$API/orcamentos/$ORC_ID/aprovar"
enviar_evento execucao.finalizada "$(jq -nc --arg os "$OS" '{osId:$os}')"
esperar_200 "$API/pagamentos/$OS" >/dev/null || { erro "cobrança não apareceu"; exit 1; }
ok "cobrança Pendente criada"

passo "1) Primeira tentativa: cartão recusado (rejected)"
PID1=$(curl -s -X POST "$API/dev/mp/pagar" -H 'Content-Type: application/json' -d "$(jq -nc --arg os "$OS" '{osId:$os,status:"rejected"}')" | jq -r .paymentId)
chamar_webhook "$PID1" >/dev/null
esperar_campo "$API/pagamentos/$OS" .status Recusado 10 && ok "status Recusado" || erro "esperava Recusado"

passo "2) Cliente tenta outro cartão na mesma preferência: aprovado (novo payment id)"
PID2=$(curl -s -X POST "$API/dev/mp/pagar" -H 'Content-Type: application/json' -d "$(jq -nc --arg os "$OS" '{osId:$os,status:"approved"}')" | jq -r .paymentId)
chamar_webhook "$PID2" >/dev/null
esperar_campo "$API/pagamentos/$OS" .status Confirmado 10 && ok "status Confirmado (o dinheiro entrou, então não pode ficar Recusado)" || erro "esperava Confirmado"

passo "3) Eventos publicados (note: pagamento.recusado e depois pagamento.confirmado)"
aviso "O OS Service precisa tolerar receber 'recusado' e depois 'confirmado' para a mesma OS."
sleep 6
./kit/scripts/read-published.sh | grep -F "$OS" || true
