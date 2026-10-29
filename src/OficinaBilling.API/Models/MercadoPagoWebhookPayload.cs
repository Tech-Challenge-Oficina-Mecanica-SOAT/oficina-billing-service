using System.Text.Json;

namespace OficinaBilling.API.Models;

/// <summary>Corpo da notificação v1 do Mercado Pago: {"action":"payment.updated","type":"payment","data":{"id":"123"}}.</summary>
public sealed class MercadoPagoWebhookPayload
{
    public string? Action { get; set; }
    public string? Type { get; set; }
    public WebhookData? Data { get; set; }
}

public sealed class WebhookData
{
    // O MP envia o id ora como string, ora como número.
    public JsonElement? Id { get; set; }

    public string? IdComoTexto() => Id?.ToString();
}
