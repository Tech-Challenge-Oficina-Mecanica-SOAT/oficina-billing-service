using System.Collections.Concurrent;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

/// <summary>
/// Substituto do Mercado Pago para dev local e testes BDD (MercadoPago:UseFake=true).
/// Para simular o webhook, use um id no formato <c>fake-{status}-{pagamentoId}</c>,
/// ex.: <c>fake-approved-3fa85f64-...</c> ou <c>fake-rejected-3fa85f64-...</c>.
/// </summary>
public sealed class FakeMercadoPagoGateway : IMercadoPagoGateway
{
    private readonly ConcurrentDictionary<Guid, decimal> _cobrancas = new();

    public ConcurrentBag<string> Estornos { get; } = new();

    public Task<CobrancaCriada> CriarCobrancaAsync(Guid pagamentoId, decimal valor, string descricao, CancellationToken ct = default)
    {
        _cobrancas[pagamentoId] = valor;
        return Task.FromResult(new CobrancaCriada($"fake-pref-{pagamentoId}", $"https://sandbox.mercadopago.local/checkout/{pagamentoId}"));
    }

    public Task<PagamentoMercadoPago?> ObterPagamentoAsync(string mercadoPagoPaymentId, CancellationToken ct = default)
    {
        // formato: fake-{status}-{guid}
        var partes = mercadoPagoPaymentId.Split('-', 3);
        if (partes.Length != 3 || partes[0] != "fake"
            || !Guid.TryParse(partes[2], out var pagamentoId)
            || !_cobrancas.TryGetValue(pagamentoId, out var valor))
            return Task.FromResult<PagamentoMercadoPago?>(null);

        return Task.FromResult<PagamentoMercadoPago?>(
            new PagamentoMercadoPago(mercadoPagoPaymentId, partes[1], pagamentoId.ToString(), valor));
    }

    public Task EstornarAsync(string mercadoPagoPaymentId, string idempotencyKey, CancellationToken ct = default)
    {
        Estornos.Add(mercadoPagoPaymentId);
        return Task.CompletedTask;
    }
}
