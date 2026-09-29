using Microsoft.EntityFrameworkCore;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Persistence.Repositories;

public class PagamentoRepository : IPagamentoRepository
{
    private readonly BillingDbContext _context;

    public PagamentoRepository(BillingDbContext context) => _context = context;

    public Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Pagamentos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Pagamento?> ObterPorOsIdAsync(Guid osId, CancellationToken ct = default) =>
        _context.Pagamentos.FirstOrDefaultAsync(p => p.OsId == osId, ct);

    public Task<Pagamento?> ObterPorPreferenceIdAsync(string preferenceId, CancellationToken ct = default) =>
        _context.Pagamentos.FirstOrDefaultAsync(p => p.PreferenceId == preferenceId, ct);

    public async Task AdicionarAsync(Pagamento pagamento, CancellationToken ct = default) =>
        await _context.Pagamentos.AddAsync(pagamento, ct);
}
