using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Domain.Interfaces;

public interface IOrcamentoRepository
{
    Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Orcamento?> ObterPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken ct = default);
    Task AdicionarAsync(Orcamento orcamento, CancellationToken ct = default);
    Task SalvarAlteracoesAsync(CancellationToken ct = default);
}
