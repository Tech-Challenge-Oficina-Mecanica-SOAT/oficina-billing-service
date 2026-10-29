namespace OficinaBilling.Application.Interfaces;

// Porta para o Mercado Pago. Isola o Application do SDK externo.
public interface IMercadoPagoGateway
{
    /// <summary>Cria a preferência (Checkout Pro). <paramref name="pagamentoId"/> vira o external_reference.</summary>
    Task<CobrancaCriada> CriarCobrancaAsync(Guid pagamentoId, decimal valor, string descricao, CancellationToken ct = default);

    /// <summary>Consulta o pagamento no MP — fonte da verdade, nunca o payload do webhook.</summary>
    Task<PagamentoMercadoPago?> ObterPagamentoAsync(string mercadoPagoPaymentId, CancellationToken ct = default);

    Task EstornarAsync(string mercadoPagoPaymentId, string idempotencyKey, CancellationToken ct = default);
}

public sealed record CobrancaCriada(string PreferenceId, string LinkPagamento);

public sealed record PagamentoMercadoPago(string Id, string Status, string? ExternalReference, decimal Valor);
