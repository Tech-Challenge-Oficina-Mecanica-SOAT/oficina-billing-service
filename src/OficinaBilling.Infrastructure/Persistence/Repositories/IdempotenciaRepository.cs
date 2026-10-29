using Microsoft.EntityFrameworkCore;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Persistence.Repositories;

public class IdempotenciaRepository : IIdempotenciaRepository
{
    private readonly BillingDbContext _context;

    public IdempotenciaRepository(BillingDbContext context) => _context = context;

    public Task<bool> JaProcessadoAsync(string chave, CancellationToken ct = default) =>
        _context.EventosProcessados.AnyAsync(e => e.Chave == chave, ct);

    public async Task MarcarProcessadoAsync(string chave, CancellationToken ct = default)
    {
        await _context.EventosProcessados.AddAsync(EventoProcessado.Criar(chave), ct);
        await _context.SaveChangesAsync(ct);
    }
}
