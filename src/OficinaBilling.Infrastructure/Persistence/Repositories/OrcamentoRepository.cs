using Microsoft.EntityFrameworkCore;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Persistence.Repositories;

public class OrcamentoRepository : IOrcamentoRepository
{
    private readonly BillingDbContext _context;

    public OrcamentoRepository(BillingDbContext context) => _context = context;

    public Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Orcamentos.Include(o => o.Itens).FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Orcamento?> ObterPorOsIdAsync(Guid osId, CancellationToken ct = default) =>
        _context.Orcamentos.Include(o => o.Itens).FirstOrDefaultAsync(o => o.OsId == osId, ct);

    public async Task AdicionarAsync(Orcamento orcamento, CancellationToken ct = default) =>
        await _context.Orcamentos.AddAsync(orcamento, ct);
}
