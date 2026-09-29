namespace OficinaBilling.Application.DTOs.Requests;

// Payload cru do POST /pagamentos/webhook/mercado-pago + a X-Idempotency-Key do header.
public sealed record WebhookMercadoPagoRequest(string IdempotencyKey, string MercadoPagoPaymentId);
