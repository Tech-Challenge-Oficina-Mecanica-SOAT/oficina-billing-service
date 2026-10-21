namespace OficinaBilling.Infrastructure.ExternalServices;

// NotificationUrl = URL PÚBLICA do webhook (ngrok em dev, ALB em homolog/prod), por exemplo
// https://abc123.ngrok-free.app/pagamentos/webhook/mercado-pago?source_news=webhooks
// Sem ela o Mercado Pago não tem para onde notificar. Null/vazio = não envia (só serve p/ teste).
public sealed record MercadoPagoOptions(string? NotificationUrl);
