using MercadoPago.Client;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using Microsoft.Extensions.Options;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Infrastructure.Configuration;

namespace OficinaBilling.Infrastructure.ExternalServices;

// Adapter do SDK oficial do Mercado Pago (Checkout Pro).
//  - CriarCobranca: cria a preferência; o Id do nosso Pagamento vai em external_reference
//    (é por ele que o webhook reencontra o pagamento).
//  - ObterPagamento: consulta autoritativa do status (o webhook só avisa "algo mudou").
//  - Estornar: devolve o valor integral (compensação do Saga).
// O token é enviado por requisição (RequestOptions) — sem estado global estático.
public sealed class MercadoPagoGateway : IMercadoPagoGateway
{
    private readonly MercadoPagoOptions _options;

    public MercadoPagoGateway(IOptions<MercadoPagoOptions> options) => _options = options.Value;

    public async Task<CobrancaCriada> CriarCobrancaAsync(Guid pagamentoId, decimal valor, string descricao, CancellationToken ct = default)
    {
        var request = new PreferenceRequest
        {
            ExternalReference = pagamentoId.ToString(),
            NotificationUrl = _options.NotificationUrl,
            Items = new List<PreferenceItemRequest>
            {
                new()
                {
                    Id = pagamentoId.ToString(),
                    Title = descricao,
                    Quantity = 1,
                    CurrencyId = "BRL",
                    UnitPrice = valor
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(_options.BackUrlSucesso))
        {
            request.BackUrls = new PreferenceBackUrlsRequest
            {
                Success = _options.BackUrlSucesso,
                Failure = _options.BackUrlFalha ?? _options.BackUrlSucesso,
                Pending = _options.BackUrlPendente ?? _options.BackUrlSucesso
            };
            request.AutoReturn = "approved";
        }

        var preferencia = await new PreferenceClient().CreateAsync(request, Opcoes(pagamentoId.ToString()), ct);

        var link = _options.UseSandbox ? preferencia.SandboxInitPoint : preferencia.InitPoint;
        return new CobrancaCriada(preferencia.Id, link);
    }

    public async Task<PagamentoMercadoPago?> ObterPagamentoAsync(string mercadoPagoPaymentId, CancellationToken ct = default)
    {
        if (!long.TryParse(mercadoPagoPaymentId, out var id))
            return null;

        try
        {
            var pagamento = await new PaymentClient().GetAsync(id, Opcoes(null), ct);
            return new PagamentoMercadoPago(
                pagamento.Id?.ToString() ?? mercadoPagoPaymentId,
                pagamento.Status ?? "unknown",
                pagamento.ExternalReference,
                pagamento.TransactionAmount ?? 0m);
        }
        catch (MercadoPago.Error.MercadoPagoApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task EstornarAsync(string mercadoPagoPaymentId, string idempotencyKey, CancellationToken ct = default)
    {
        await new PaymentRefundClient().RefundAsync(long.Parse(mercadoPagoPaymentId), Opcoes(idempotencyKey), ct);
    }

    private RequestOptions Opcoes(string? idempotencyKey)
    {
        var opcoes = new RequestOptions { AccessToken = _options.AccessToken };
        if (idempotencyKey is not null)
            opcoes.CustomHeaders.Add("X-Idempotency-Key", idempotencyKey);
        return opcoes;
    }
}
