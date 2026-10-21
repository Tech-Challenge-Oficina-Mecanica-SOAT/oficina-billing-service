namespace OficinaBilling.Application.Interfaces;

// Porta para o SDK do Mercado Pago (Checkout Pro / preferências). Isola o
// Application do pacote externo — Infrastructure é quem chama o SDK de fato.
public interface IMercadoPagoGateway
{
    Task<PreferenciaCriada> CriarPreferenciaAsync(decimal valor, string descricao, Guid referenciaExterna, CancellationToken ct = default);

    // Usado no webhook para confirmar o status real antes de aceitar o que veio no payload
    // (nunca confiar cegamente em notificação de webhook). Retorna null se o payment não existe.
    Task<StatusPagamentoMp?> ConsultarPagamentoAsync(string mercadoPagoPaymentId, CancellationToken ct = default);
}

public sealed record PreferenciaCriada(string PreferenceId, string LinkPagamento);

// ExternalReference = o que enviamos ao criar a preferência (o Id do Orçamento). É o elo entre
// o "payment id" que chega no webhook e o Pagamento salvo aqui.
public sealed record StatusPagamentoMp(string MercadoPagoPaymentId, string Status, decimal Valor, string? ExternalReference);
