using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// Envolve o SDK oficial do Mercado Pago (pacote "mercadopago-sdk").
// O access token é setado globalmente em Program.cs a partir do Secrets
// Manager (ver terraform/secrets.tf) — nunca hardcoded.
public class MercadoPagoGateway : IMercadoPagoGateway
{
    private readonly PreferenceClient _preferenceClient;
    private readonly PaymentClient _paymentClient;

    public MercadoPagoGateway(PreferenceClient preferenceClient, PaymentClient paymentClient)
    {
        _preferenceClient = preferenceClient;
        _paymentClient = paymentClient;
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
            ExternalReference = referenciaExterna.ToString(),
            // TODO: preencher com a URL pública do webhook (ngrok em dev, ALB em homolog/prod) —
            // ver plano-tavares-billing-service.md, seção "cuidados".
            NotificationUrl = null
        };

        var preferencia = await _preferenceClient.CreateAsync(request, cancellationToken: ct);

        return new PreferenciaCriada(preferencia.Id, preferencia.InitPoint);
    }

    public async Task<StatusPagamentoMp> ConsultarPagamentoAsync(string mercadoPagoPaymentId, CancellationToken ct = default)
    {
        if (!long.TryParse(mercadoPagoPaymentId, out var paymentId))
            throw new ArgumentException($"'{mercadoPagoPaymentId}' não é um payment id válido do Mercado Pago.", nameof(mercadoPagoPaymentId));

        var pagamento = await _paymentClient.GetAsync(paymentId, cancellationToken: ct);

        return new StatusPagamentoMp(pagamento.Id.ToString()!, pagamento.Status, pagamento.TransactionAmount ?? 0m);
    }
}
