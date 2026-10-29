using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OficinaBilling.API.Models;
using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.UseCases.Pagamento.ConsultarPagamento;
using OficinaBilling.Application.UseCases.Pagamento.ProcessarWebhookMercadoPago;
using OficinaBilling.Infrastructure.Configuration;

namespace OficinaBilling.API.Controllers;

[ApiController]
[Route("pagamentos")]
[Produces("application/json")]
public class PagamentosController : ControllerBase
{
    /// <summary>Consulta o pagamento da OS (inclui o link de pagamento do Mercado Pago).</summary>
    [HttpGet("{osId:guid}")]
    [ProducesResponseType(typeof(PagamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Consultar(Guid osId, [FromServices] ConsultarPagamentoUseCase useCase, CancellationToken ct) =>
        (await useCase.ExecutarAsync(osId, ct)).ParaActionResult(this);

    /// <summary>
    /// Webhook do Mercado Pago. Idempotente: o MP reenvia a notificação até receber 2xx.
    /// Responde 200 para tudo que não adianta reenviar (tipo ignorado, pagamento desconhecido,
    /// duplicata) e deixa exceções virarem 5xx para o MP tentar de novo.
    /// </summary>
    [HttpPost("webhook/mercado-pago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> WebhookMercadoPago(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] MercadoPagoWebhookPayload? payload,
        [FromQuery(Name = "data.id")] string? dataIdQuery,
        [FromQuery] string? id,
        [FromQuery] string? topic,
        [FromQuery] string? type,
        [FromServices] ProcessarWebhookMercadoPagoUseCase useCase,
        [FromServices] IIdempotencyStore idempotencia,
        [FromServices] IOptions<MercadoPagoOptions> opcoes,
        [FromServices] IAppLogger<PagamentosController> logger,
        CancellationToken ct)
    {
        var tipo = payload?.Type ?? type ?? topic;
        if (!string.Equals(tipo, "payment", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Webhook do tipo '{Tipo}' ignorado.", tipo ?? "(vazio)");
            return Ok();
        }

        var paymentId = payload?.Data?.IdComoTexto() ?? dataIdQuery ?? id;
        if (string.IsNullOrWhiteSpace(paymentId))
            return BadRequest(new ProblemDetails { Detail = "Id do pagamento ausente (data.id)." });

        var config = opcoes.Value;
        if (config.ValidateSignature)
        {
            var valida = MercadoPagoWebhookSignature.Validar(
                config.WebhookSecret,
                Request.Headers["x-signature"].FirstOrDefault(),
                Request.Headers["x-request-id"].FirstOrDefault(),
                dataIdQuery ?? paymentId);

            if (!valida)
            {
                logger.LogWarning("Webhook com assinatura inválida rejeitado (payment {PaymentId}).", paymentId);
                return Unauthorized();
            }
        }

        // Idempotency-Key: o cliente (ou o próprio MP, via x-request-id) identifica a entrega.
        var chave = Request.Headers["X-Idempotency-Key"].FirstOrDefault()
                    ?? Request.Headers["x-request-id"].FirstOrDefault();
        if (chave is not null)
        {
            chave = $"webhook:{chave}";
            if (await idempotencia.JaProcessadoAsync(chave, ct))
                return Ok(new { duplicado = true });
        }

        var resultado = await useCase.ExecutarAsync(paymentId, ct);

        // Falhas de negócio (pagamento desconhecido, valor divergente) não melhoram com reenvio: 200.
        // A chave só é gravada em sucesso — falha transitória vira exceção/5xx e o MP reenvia.
        if (chave is not null && resultado.Sucesso)
            await idempotencia.RegistrarAsync(chave, ct);

        return Ok(new { processado = resultado.Sucesso });
    }
}
