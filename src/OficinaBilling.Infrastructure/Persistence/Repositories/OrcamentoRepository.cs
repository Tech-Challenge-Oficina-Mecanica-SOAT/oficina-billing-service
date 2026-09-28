using Microsoft.EntityFrameworkCore;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Persistence.Repositories;

public class OrcamentoRepository : IOrcamentoRepository
{
    private readonly BillingDbContext _context;

    public OrcamentoRepository(BillingDbContext context) => _context = context;

    public Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Orcamentos.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Orcamento?> ObterPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken ct = default) =>
        _context.Orcamentos.FirstOrDefaultAsync(o => o.OrdemServicoId == ordemServicoId, ct);

    public async Task AdicionarAsync(Orcamento orcamento, CancellationToken ct = default) =>
        await _context.Orcamentos.AddAsync(orcamento, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
