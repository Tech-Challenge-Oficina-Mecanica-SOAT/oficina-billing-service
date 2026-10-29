using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.ValueObjects;
using OficinaBilling.Infrastructure.Persistence.Entities;

namespace OficinaBilling.Infrastructure.Persistence.Configurations;

public class OrcamentoConfiguration : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> b)
    {
        b.ToTable("orcamentos");
        b.HasKey(o => o.Id);
        b.Property(o => o.Id).ValueGeneratedNever();
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.ValorTotal).HasConversion(d => d.Valor, v => new Dinheiro(v)).HasColumnType("numeric(18,2)");

        // 1 orçamento por OS: garante idempotência mesmo com mensagens SQS concorrentes.
        b.HasIndex(o => o.OrdemServicoId).IsUnique();

        b.Ignore(o => o.DomainEvents);

        // Concorrência otimista via coluna de sistema xmin do PostgreSQL.
        b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        b.OwnsMany(o => o.Itens, ib =>
        {
            ib.ToTable("orcamento_itens");
            ib.WithOwner().HasForeignKey("OrcamentoId");
            ib.Property<int>("Id");
            ib.HasKey("Id");
            ib.Property(i => i.Descricao).HasMaxLength(200);
            ib.Property(i => i.PrecoUnitario).HasConversion(d => d.Valor, v => new Dinheiro(v)).HasColumnType("numeric(18,2)");
            ib.Ignore(i => i.Subtotal);
        });
        b.Navigation(o => o.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> b)
    {
        b.ToTable("pagamentos");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Valor).HasConversion(d => d.Valor, v => new Dinheiro(v)).HasColumnType("numeric(18,2)");
        b.Property(p => p.MercadoPagoPreferenceId).HasMaxLength(100);
        b.Property(p => p.LinkPagamento).HasMaxLength(500);
        b.Property(p => p.MercadoPagoPaymentId).HasMaxLength(50);

        // No máximo uma cobrança "viva" (Pendente/Confirmado) por OS: impede cobrança em duplicidade.
        b.HasIndex(p => p.OrdemServicoId).IsUnique().HasFilter("\"Status\" IN ('Pendente','Confirmado')");
        b.HasIndex(p => p.MercadoPagoPaymentId); // reconciliação

        b.Ignore(p => p.DomainEvents);
        b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property(m => m.Topico).HasMaxLength(100);
        b.Property(m => m.Payload).HasColumnType("text");
        b.Property(m => m.CorrelationId).HasMaxLength(100);
        b.HasIndex(m => m.OcorridoEm).HasFilter("\"ProcessadoEm\" IS NULL");
    }
}

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntry> b)
    {
        b.ToTable("idempotency_keys");
        b.HasKey(k => k.Chave);
        b.Property(k => k.Chave).HasMaxLength(200);
    }
}
