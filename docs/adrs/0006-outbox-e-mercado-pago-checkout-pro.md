# ADR 0006 — Outbox Pattern e Mercado Pago Checkout Pro (decisões locais do Billing)

- **Status:** Aceito
- **Escopo:** Billing Service
- **Data:** 2026-10

## 1. Outbox Pattern

**Problema:** "gravar no banco e depois publicar no SNS" é uma escrita dupla — se o processo cair (ou o SNS falhar) entre as duas, o estado muda mas o evento nunca sai, e o Saga trava.

**Decisão:** o use case grava o agregado **e** uma linha em `outbox_messages` no mesmo `SaveChanges` (mesma transação). O `OutboxProcessor` (BackgroundService) lê as pendentes com
`SELECT … FOR UPDATE SKIP LOCKED` (várias réplicas sem publicar em duplicidade), publica no SNS e marca como processada; em erro incrementa `Tentativas` (limite 10) e registra `UltimoErro`.
Garantia *at-least-once* — consumidores deduplicam pelo `eventId` do envelope.

**Custo:** latência de ~2 s entre a decisão e a publicação (intervalo de polling configurável).

## 2. Mercado Pago — Checkout Pro

**Decisão:** ao receber `execucao.finalizada`, o Billing cria uma **preferência** (Checkout Pro) e devolve o link (`GET /pagamentos/{osId}`). O cliente paga na página do Mercado Pago.
- `external_reference` da preferência = `Pagamento.Id` — é por ele que o webhook reencontra o pagamento (o id do pagamento do MP só existe depois do pagamento).
- **O webhook nunca é fonte da verdade:** ele só avisa. O Billing consulta `GET /v1/payments/{id}` no MP, confere o **valor** pago contra o cobrado e só então confirma.
- Assinatura `x-signature` (HMAC-SHA256) validada quando `MercadoPago:ValidateSignature=true` (homolog/prod).
- Idempotência em 3 camadas: estado do domínio (`Confirmar()` só age em `Pendente`), tabela `idempotency_keys` (`X-Idempotency-Key`/`x-request-id`) e concorrência otimista (`xmin`).
- Índice único parcial: no máximo **uma** cobrança Pendente/Confirmada por OS (impede cobrança em duplicidade).

## 3. Limitações conhecidas

- Uma preferência admite várias tentativas de pagamento. Tratamos `rejected` como desfecho da cobrança (publica `pagamento.recusado`); se o cliente pagar depois com sucesso na mesma preferência,
  o Billing **estorna automaticamente** (ver `ProcessarWebhookMercadoPagoUseCase`). Alternativa futura: nova cobrança a cada tentativa.
- Falha permanente na geração do orçamento (peça inexistente) vai para a DLQ após 5 tentativas, sem evento de falha dedicado; o Saga depende do alarme da DLQ. Um evento `orcamento.falhou` exigiria combinar contrato com o OS Service.
