using Microsoft.AspNetCore.Mvc;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.UseCases.Pagamento;

namespace OficinaBilling.API.Controllers;

[ApiController]
[Route("pagamentos/webhook")]
public class WebhooksController : ControllerBase
{
    private readonly ProcessarWebhookMercadoPagoUseCase _processarWebhookUseCase;

    public WebhooksController(ProcessarWebhookMercadoPagoUseCase processarWebhookUseCase) =>
        _processarWebhookUseCase = processarWebhookUseCase;

    // POST /pagamentos/webhook/mercado-pago — chamado pelo Mercado Pago.
    // Sempre responde 200 rápido (mesmo em erro de negócio) para o MP não
    // ficar reenviando em loop; erros são só logados internamente.
    [HttpPost("mercado-pago")]
    public async Task<IActionResult> MercadoPago(
        [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey,
        [FromQuery] string id, // Mercado Pago manda o payment id via query string ?id=...
        CancellationToken ct)
    {
        var request = new WebhookMercadoPagoRequest(idempotencyKey, id);
        await _processarWebhookUseCase.ExecutarAsync(request, ct);
        return Ok();
    }
}
