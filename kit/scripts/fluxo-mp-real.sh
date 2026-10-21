#!/usr/bin/env bash
# Teste com o SANDBOX REAL do Mercado Pago. Os outros serviços continuam simulados por este kit
# (Execução publicando diagnostico.concluido / execucao.finalizada), mas o pagamento é de verdade:
# preferência criada pelo SDK, checkout do MP, webhook de verdade chegando pelo ngrok.
#
# Pré-requisitos (veja docs/GUIA-DE-TESTES.md, seção 3):
#   - .env preenchido com MP_USE_FAKE=false, MP_ACCESS_TOKEN=TEST-..., MP_NOTIFICATION_URL=https://...ngrok...
#   - ngrok rodando (ngrok http 5001)
#   - docker compose up -d --force-recreate billing-api
source "$(dirname "$0")/_lib.sh"
verificar_api
OS=$(uuid)
LIMITE_SEGUNDOS="${LIMITE_SEGUNDOS:-600}"

passo "1) Simulando a Execução: diagnostico.concluido da OS $OS (R\$ 1)"
enviar_evento diagnostico.concluido "$(body_diagnostico "$OS")"
ORC_ID=$(esperar_200 "$API/orcamentos/$OS" | jq -r .id) || { erro "orçamento não apareceu (docker compose logs billing-api)"; exit 1; }
ok "orçamento $ORC_ID"

passo "2) Aprovando o orçamento (REST)"
code=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/orcamentos/$ORC_ID/aprovar")
[[ "$code" == "204" ]] && ok "HTTP 204" || { erro "HTTP $code"; exit 1; }

passo "3) Simulando a Execução: execucao.finalizada -> Billing cria a preferência no Mercado Pago"
enviar_evento execucao.finalizada "$(jq -nc --arg os "$OS" '{osId:$os}')"
PAG=$(esperar_200 "$API/pagamentos/$OS") || { erro "cobrança não apareceu (docker compose logs billing-api)"; exit 1; }
LINK=$(echo "$PAG" | jq -r .linkPagamento)
if [[ "$LINK" == *"fake-pref"* ]]; then
  erro "A API ainda está com o Mercado Pago FAKE. Ajuste o .env (MP_USE_FAKE=false) e rode:"
  echo "      docker compose up -d --force-recreate billing-api"
  exit 1
fi
echo "$PAG" | jq -c '{status, valor, linkPagamento}'

passo "4) PAGUE AGORA no navegador com o USUÁRIO COMPRADOR DE TESTE:"
echo "   $LINK"
info "No cartão de teste, o resultado é escolhido pelo NOME do titular (ex.: APRO = aprovado, OTHE = recusado)."
info "Confira a lista atual na documentação de cartões de teste do Mercado Pago."
info "Acompanhe também o painel do ngrok: http://localhost:4040"

passo "5) Aguardando o webhook (até ${LIMITE_SEGUNDOS}s)..."
ultimo=""; inicio=$SECONDS
while (( SECONDS - inicio < LIMITE_SEGUNDOS )); do
  atual=$(curl -s "$API/pagamentos/$OS" | jq -r '.status // empty')
  if [[ "$atual" != "$ultimo" ]]; then
    info "status do pagamento: $atual"
    ultimo="$atual"
  fi
  [[ "$atual" == "Confirmado" ]] && break
  sleep 3
done

if [[ "$ultimo" == "Confirmado" ]]; then
  ok "pagamento Confirmado pelo webhook real do Mercado Pago"
  curl -s "$API/pagamentos/$OS" | jq -c '{status, valor, mercadoPagoId}'
else
  erro "não chegou a Confirmado (último status: ${ultimo:-nenhum})."
  echo "   Veja: docker compose logs --tail=80 billing-api   e o painel do ngrok (http://localhost:4040)"
  echo "   Se o ngrok mostra o POST do MP com 401, desligue a assinatura (MP_VALIDATE_SIGNATURE=false) para isolar."
fi

passo "6) Eventos que o Billing publicou (espere ~6s pelo worker da Outbox)"
sleep 6
./kit/scripts/read-published.sh | grep -F "$OS" || true
