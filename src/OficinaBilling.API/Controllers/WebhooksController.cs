using Microsoft.AspNetCore.Mvc;
using OficinaBilling.Application.UseCases.Pagamento.ProcessarWebhookMercadoPago;

namespace OficinaBilling.API.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly ProcessarWebhookMercadoPagoUseCase _processarWebhookUseCase;

    public WebhooksController(ProcessarWebhookMercadoPagoUseCase processarWebhookUseCase)
    {
        _processarWebhookUseCase = processarWebhookUseCase;
    }

    // Endpoint público chamado pelo Mercado Pago. Sempre responder 200 rápido
    // (mesmo em caso de erro de negócio) para o MP não ficar reenviando em loop;
    // logue o erro internamente em vez de propagar como 4xx/5xx para o webhook.
    [HttpPost("mercadopago")]
    public async Task<IActionResult> MercadoPago([FromQuery] string id, CancellationToken ct)
    {
        await _processarWebhookUseCase.ExecutarAsync(id, ct);
        return Ok();
    }
}
