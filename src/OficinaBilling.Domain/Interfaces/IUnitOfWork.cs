namespace OficinaBilling.Domain.Interfaces;

// Garante que a mudança no agregado + a linha na Outbox sejam gravadas na
// mesma transação (é isso que faz o Outbox Pattern funcionar de verdade).
public interface IUnitOfWork
{
    Task SalvarAlteracoesAsync(CancellationToken ct = default);
}
