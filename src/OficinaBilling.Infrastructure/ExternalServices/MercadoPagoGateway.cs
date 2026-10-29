using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// Envolve o SDK oficial do Mercado Pago (pacote "mercadopago-sdk").
// O access token é setado globalmente em Program.cs a partir da configuração
// (Secrets Manager em produção, ver terraform/secrets.tf) — nunca hardcoded.
public class MercadoPagoGateway : IMercadoPagoGateway
{
    private readonly PreferenceClient _preferenceClient;
    private readonly PaymentClient _paymentClient;
    private readonly MercadoPagoOptions _options;

    public MercadoPagoGateway(PreferenceClient preferenceClient, PaymentClient paymentClient, MercadoPagoOptions options)
    {
        _preferenceClient = preferenceClient;
        _paymentClient = paymentClient;
        _options = options;
    }

    public async Task<PreferenciaCriada> CriarPreferenciaAsync(decimal valor, string descricao, Guid referenciaExterna, CancellationToken ct = default)
    {
        var request = new PreferenceRequest
        {
            Items = new List<PreferenceItemRequest>
            {
                new()
                {
                    Title = descricao,
                    Quantity = 1,
                    CurrencyId = "BRL",
                    UnitPrice = valor
                }
            },
            // É isto que liga o pagamento ao nosso Orçamento quando o webhook chegar.
            ExternalReference = referenciaExterna.ToString(),
            NotificationUrl = string.IsNullOrWhiteSpace(_options.NotificationUrl) ? null : _options.NotificationUrl
        };

        var preferencia = await _preferenceClient.CreateAsync(request, cancellationToken: ct);

        return new PreferenciaCriada(preferencia.Id, preferencia.InitPoint);
    }

    public async Task<StatusPagamentoMp?> ConsultarPagamentoAsync(string mercadoPagoPaymentId, CancellationToken ct = default)
    {
        if (!long.TryParse(mercadoPagoPaymentId, out var paymentId))
            throw new ArgumentException($"'{mercadoPagoPaymentId}' não é um payment id válido do Mercado Pago.", nameof(mercadoPagoPaymentId));

        var pagamento = await _paymentClient.GetAsync(paymentId, cancellationToken: ct);

        return new StatusPagamentoMp(
            pagamento.Id.ToString()!,
            pagamento.Status,
            pagamento.TransactionAmount ?? 0m,
            pagamento.ExternalReference);
    }
}
