namespace OficinaBilling.Application.DTOs.Requests;

// O que importa do webhook do Mercado Pago: só o id do payment. O resto (status, valor,
// external_reference) é buscado na API do MP — nunca confiamos no corpo da notificação.
public sealed record WebhookMercadoPagoRequest(string MercadoPagoPaymentId);
