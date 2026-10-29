using Microsoft.EntityFrameworkCore;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Infrastructure.Persistence.Entities;

namespace OficinaBilling.Infrastructure.Persistence;

public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly BillingDbContext _context;

    public IdempotencyStore(BillingDbContext context) => _context = context;

    public Task<bool> JaProcessadoAsync(string chave, CancellationToken ct = default) =>
        _context.IdempotencyKeys.AsNoTracking().AnyAsync(k => k.Chave == chave, ct);

    public async Task RegistrarAsync(string chave, CancellationToken ct = default)
    {
        _context.IdempotencyKeys.Add(new IdempotencyKeyEntry { Chave = chave, ProcessadoEm = DateTime.UtcNow });
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Outra requisição registrou a mesma chave primeiro: objetivo já cumprido.
            _context.ChangeTracker.Clear();
        }
    }
}
