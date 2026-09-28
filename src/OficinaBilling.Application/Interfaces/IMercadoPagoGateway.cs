namespace OficinaBilling.Application.Interfaces;

// Porta para o SDK do Mercado Pago. Isola o Application do pacote externo.
public interface IMercadoPagoGateway
{
    Task<CobrancaCriada> CriarCobrancaAsync(decimal valor, string descricao, CancellationToken ct = default);
}

public sealed record CobrancaCriada(string MercadoPagoPaymentId, string LinkPagamento);
