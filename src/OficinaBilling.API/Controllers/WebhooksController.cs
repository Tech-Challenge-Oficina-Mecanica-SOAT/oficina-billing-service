using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.Security;
using OficinaBilling.Application.UseCases.Pagamento;

namespace OficinaBilling.API.Controllers;

[ApiController]
[Route("pagamentos/webhook")]
public class WebhooksController : ControllerBase
{
    private readonly ProcessarWebhookMercadoPagoUseCase _processarWebhookUseCase;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        ProcessarWebhookMercadoPagoUseCase processarWebhookUseCase,
        IConfiguration configuration,
        ILogger<WebhooksController> logger)
    {
        _processarWebhookUseCase = processarWebhookUseCase;
        _configuration = configuration;
        _logger = logger;
    }

    // POST /pagamentos/webhook/mercado-pago — chamado pelo Mercado Pago.
    //
    // O MP NÃO manda X-Idempotency-Key. O que chega é:
    //   headers: x-signature (HMAC), x-request-id
    //   query  : ?data.id=<paymentId>&type=payment        (webhooks)
    //            ?id=<paymentId>&topic=payment             (IPN legado, sem assinatura)
    //   body   : {"type":"payment","action":"payment.updated","data":{"id":"123"}, ...}
    //
    // Respostas:
    //   401 -> assinatura inválida/ausente (quando MercadoPago:ValidateSignature=true)
    //   200 -> aceito, ignorado (outro tipo de evento) ou erro de NEGÓCIO (só logado; reenviar não adianta)
    //   500 -> exceção inesperada (ex.: API do MP fora do ar): o MP reenvia, e é isso que queremos
    [HttpPost("mercado-pago")]
    public async Task<IActionResult> MercadoPago(CancellationToken ct)
    {
        var body = await LerCorpoAsync(ct);

        var dataIdQuery = Request.Query["data.id"].FirstOrDefault();
        var dataId = dataIdQuery
                     ?? Request.Query["id"].FirstOrDefault()
                     ?? body?.DataId;

        if (_configuration.GetValue("MercadoPago:ValidateSignature", true))
        {
            var segredo = _configuration["MercadoPago:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(segredo))
            {
                _logger.LogError("MercadoPago:ValidateSignature está ligado, mas MercadoPago:WebhookSecret não foi configurado.");
                return Unauthorized();
            }

            var valida = MercadoPagoWebhookSignature.Validar(
                segredo,
                dataIdQuery ?? dataId,
                Request.Headers["x-request-id"].FirstOrDefault(),
                Request.Headers["x-signature"].FirstOrDefault());

            if (!valida)
            {
                _logger.LogWarning("Webhook do Mercado Pago com assinatura inválida ou ausente (data.id={DataId}).", dataId);
                return Unauthorized();
            }
        }

        var tipo = Request.Query["type"].FirstOrDefault() ?? Request.Query["topic"].FirstOrDefault() ?? body?.Type;
        if (tipo is not null && tipo != "payment")
        {
            _logger.LogInformation("Webhook do Mercado Pago do tipo '{Tipo}' ignorado (só tratamos 'payment').", tipo);
            return Ok();
        }

        if (string.IsNullOrWhiteSpace(dataId))
        {
            _logger.LogWarning("Webhook do Mercado Pago sem payment id (nem em data.id, nem em id, nem no corpo).");
            return Ok();
        }

        var resultado = await _processarWebhookUseCase.ExecutarAsync(new WebhookMercadoPagoRequest(dataId), ct);
        if (!resultado.Sucesso)
            _logger.LogWarning("Webhook do payment {PaymentId} não aplicado: {Erro}", dataId, resultado.Erro);

        return Ok();
    }

    // Lê só o que interessa do corpo; tolera corpo vazio/inválido e data.id numérico ou string.
    private async Task<WebhookBody?> LerCorpoAsync(CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            string? tipo = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

            string? id = null;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("id", out var idEl))
            {
                id = idEl.ValueKind switch
                {
                    JsonValueKind.String => idEl.GetString(),
                    JsonValueKind.Number => idEl.GetRawText(),
                    _ => null
                };
            }

            return new WebhookBody(tipo, id);
        }
        catch (JsonException)
        {
            return null; // corpo vazio ou inválido: os dados podem estar só na query string
        }
    }

    private sealed record WebhookBody(string? Type, string? DataId);
}
