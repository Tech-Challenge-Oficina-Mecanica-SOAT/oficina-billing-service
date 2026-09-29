namespace OficinaBilling.Application.Interfaces;

// Porta para o SDK do Mercado Pago (Checkout Pro / preferências). Isola o
// Application do pacote externo — Infrastructure é quem chama o SDK de fato.
public interface IMercadoPagoGateway
{
    Task<PreferenciaCriada> CriarPreferenciaAsync(decimal valor, string descricao, Guid referenciaExterna, CancellationToken ct = default);

    // Usado no webhook para confirmar o status real antes de aceitar o que veio no payload
    // (nunca confiar cegamente em notificação de webhook).
    Task<StatusPagamentoMp> ConsultarPagamentoAsync(string mercadoPagoPaymentId, CancellationToken ct = default);
}

public sealed record PreferenciaCriada(string PreferenceId, string LinkPagamento);

public sealed record StatusPagamentoMp(string MercadoPagoPaymentId, string Status, decimal Valor);
