using Microsoft.EntityFrameworkCore;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Persistence.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly BillingDbContext _context;

    public OutboxRepository(BillingDbContext context) => _context = context;

    public async Task AdicionarAsync(OutboxMessage mensagem, CancellationToken ct = default) =>
        await _context.OutboxMessages.AddAsync(mensagem, ct);

    public Task<List<OutboxMessage>> ObterPendentesAsync(int limite = 20, CancellationToken ct = default) =>
        _context.OutboxMessages
            .Where(m => m.PublicadoEm == null)
            .OrderBy(m => m.CriadoEm)
            .Take(limite)
            .ToListAsync(ct);

    public async Task MarcarPublicadaAsync(Guid id, CancellationToken ct = default)
    {
        var mensagem = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct);
        mensagem?.MarcarPublicada();
        await _context.SaveChangesAsync(ct); // o worker de outbox não passa pelo IUnitOfWork "de negócio"
    }

    public async Task RegistrarFalhaAsync(Guid id, string erro, CancellationToken ct = default)
    {
        var mensagem = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct);
        mensagem?.RegistrarFalha(erro);
        await _context.SaveChangesAsync(ct);
    }
}
