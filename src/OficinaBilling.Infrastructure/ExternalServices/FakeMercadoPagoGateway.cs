using System.Collections.Concurrent;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// SÓ PARA TESTE LOCAL (MercadoPago:UseFake=true, e apenas em Development — ver Program.cs).
// Substitui o Mercado Pago real: cria "preferências" em memória e deixa o endpoint
// POST /dev/mp/pagar registrar o resultado de um pagamento (approved/rejected/...), para
// você exercitar o webhook ponta a ponta sem credenciais, ngrok nem internet.
public class FakeMercadoPagoGateway : IMercadoPagoGateway
{
    private readonly ConcurrentDictionary<string, StatusPagamentoMp> _pagamentos = new();
    private long _proximoPaymentId = 1_000_000;

    public Task<PreferenciaCriada> CriarPreferenciaAsync(decimal valor, string descricao, Guid referenciaExterna, CancellationToken ct = default)
    {
        var preferenceId = $"fake-pref-{referenciaExterna:N}";
        return Task.FromResult(new PreferenciaCriada(preferenceId, $"http://localhost:5001/dev/mp/checkout/{preferenceId}"));
    }

    public Task<StatusPagamentoMp?> ConsultarPagamentoAsync(string mercadoPagoPaymentId, CancellationToken ct = default) =>
        Task.FromResult(_pagamentos.TryGetValue(mercadoPagoPaymentId, out var p) ? p : null);

    // Simula "o cliente pagou (ou tentou pagar) no checkout do MP".
    public StatusPagamentoMp RegistrarPagamento(string? paymentId, string status, decimal valor, string externalReference)
    {
        var id = string.IsNullOrWhiteSpace(paymentId) ? Interlocked.Increment(ref _proximoPaymentId).ToString() : paymentId;
        var pagamento = new StatusPagamentoMp(id, status, valor, externalReference);
        _pagamentos[id] = pagamento;
        return pagamento;
    }
}
