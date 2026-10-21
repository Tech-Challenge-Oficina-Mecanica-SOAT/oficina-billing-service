using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Domain.Interfaces;

public interface IPagamentoRepository
{
    Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Pagamento?> ObterPorOsIdAsync(Guid osId, CancellationToken ct = default);
    Task<Pagamento?> ObterPorPreferenceIdAsync(string preferenceId, CancellationToken ct = default);
    Task<Pagamento?> ObterPorOrcamentoIdAsync(Guid orcamentoId, CancellationToken ct = default);
    Task AdicionarAsync(Pagamento pagamento, CancellationToken ct = default);
}
