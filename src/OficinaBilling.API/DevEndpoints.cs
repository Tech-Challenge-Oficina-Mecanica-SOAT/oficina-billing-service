using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.UseCases.Orcamento;
using OficinaBilling.Application.UseCases.Pagamento;
using OficinaBilling.Domain.Interfaces;
using OficinaBilling.Infrastructure.ExternalServices;

namespace OficinaBilling.API;

// Endpoints SÓ PARA TESTE LOCAL. Só são mapeados em Development E com Dev:EnableTestEndpoints=true
// (ver Program.cs). Servem para exercitar o serviço sem SQS/SNS e sem o Mercado Pago real:
//   POST /dev/eventos/diagnostico-concluido   -> o mesmo que o consumer faz ao receber o evento
//   POST /dev/eventos/execucao-finalizada     -> idem (cria a cobrança)
//   POST /dev/eventos/os-cancelada            -> idem (compensação)
//   POST /dev/mp/pagar                        -> (MercadoPago:UseFake=true) simula o cliente pagando
public static class DevEndpoints
{
    public static void MapDevEndpoints(this WebApplication app)
    {
        var dev = app.MapGroup("/dev").WithTags("DEV (apenas teste local)");

        dev.MapPost("/eventos/diagnostico-concluido", async (GerarOrcamentoRequest body, GerarOrcamentoUseCase useCase, CancellationToken ct) =>
        {
            var r = await useCase.ExecutarAsync(body, ct);
            return r.Sucesso ? Results.Ok(r.Valor) : Results.UnprocessableEntity(new { erro = r.Erro });
        });

        dev.MapPost("/eventos/execucao-finalizada", async (OsIdBody body, CriarCobrancaUseCase useCase, CancellationToken ct) =>
        {
            var r = await useCase.ExecutarAsync(body.OsId, ct);
            return r.Sucesso ? Results.Ok(r.Valor) : Results.UnprocessableEntity(new { erro = r.Erro });
        });

        dev.MapPost("/eventos/os-cancelada", async (OsIdBody body, CancelarOrcamentoPorOsUseCase useCase, CancellationToken ct) =>
        {
            await useCase.ExecutarAsync(body.OsId, ct);
            return Results.NoContent();
        });

        // status: approved | rejected | cancelled | pending | in_process. valor: opcional (default = valor cobrado).
        dev.MapPost("/mp/pagar", async (PagarBody body, IPagamentoRepository pagamentos, IMercadoPagoGateway gateway, CancellationToken ct) =>
        {
            if (gateway is not FakeMercadoPagoGateway fake)
                return Results.Conflict(new { erro = "Disponível apenas com MercadoPago:UseFake=true." });

            var pagamento = await pagamentos.ObterPorOsIdAsync(body.OsId, ct);
            if (pagamento is null)
                return Results.NotFound(new { erro = $"Nenhum pagamento para a OS {body.OsId}. Crie a cobrança antes (execucao.finalizada)." });

            var status = string.IsNullOrWhiteSpace(body.Status) ? "approved" : body.Status;
            var criado = fake.RegistrarPagamento(body.PaymentId, status, body.Valor ?? pagamento.Valor.Valor, pagamento.OrcamentoId.ToString());

            return Results.Ok(new
            {
                paymentId = criado.MercadoPagoPaymentId,
                criado.Status,
                criado.Valor,
                externalReference = criado.ExternalReference,
                proximoPasso = $"POST /pagamentos/webhook/mercado-pago?data.id={criado.MercadoPagoPaymentId}&type=payment"
            });
        });
    }

    public sealed record OsIdBody(Guid OsId);
    public sealed record PagarBody(Guid OsId, string? Status, string? PaymentId, decimal? Valor);
}
