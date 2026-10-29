using Microsoft.EntityFrameworkCore;
using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Infrastructure.Persistence;

// Conecta no database lógico "oficina_billing" dentro do RDS compartilhado
// (criado pelo repositório oficina-infra-db). O usuário do Postgres usado
// aqui só tem permissão nesse database — ver terraform/database.tf.
public class BillingDbContext : DbContext
{
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<EventoProcessado> EventosProcessados => Set<EventoProcessado>();

    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
