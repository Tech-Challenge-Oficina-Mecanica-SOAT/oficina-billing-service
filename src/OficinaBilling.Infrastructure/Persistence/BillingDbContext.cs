using Microsoft.EntityFrameworkCore;
using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Infrastructure.Persistence;

// Conecta no database lógico "oficina_billing" dentro do mesmo RDS
// compartilhado (ver terraform/database.tf). O usuário do Postgres usado
// aqui só tem permissão nesse database — nunca em oficina_os, oficina_cadastros etc.
public class BillingDbContext : DbContext
{
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();

    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
