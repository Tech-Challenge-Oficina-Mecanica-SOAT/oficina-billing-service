using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Domain.Interfaces;

public interface IPagamentoRepository
{
    Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Pagamento?> ObterPorMercadoPagoIdAsync(string mercadoPagoPaymentId, CancellationToken ct = default);
    Task AdicionarAsync(Pagamento pagamento, CancellationToken ct = default);
    Task SalvarAlteracoesAsync(CancellationToken ct = default);
}
