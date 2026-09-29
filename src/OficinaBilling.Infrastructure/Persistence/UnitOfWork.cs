using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly BillingDbContext _context;

    public UnitOfWork(BillingDbContext context) => _context = context;

    // Garante que o agregado (Orcamento/Pagamento) e a linha da Outbox gravada
    // no mesmo request caiam na MESMA transação — é isso que faz o Outbox Pattern funcionar.
    public Task SalvarAlteracoesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
