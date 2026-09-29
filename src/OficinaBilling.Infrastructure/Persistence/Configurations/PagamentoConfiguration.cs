using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Infrastructure.Persistence.Configurations;

public class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("pagamentos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.OsId).IsRequired();
        builder.Property(p => p.OrcamentoId).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.PreferenceId).HasMaxLength(100);
        builder.Property(p => p.MercadoPagoId).HasMaxLength(100);
        builder.Property(p => p.LinkPagamento).HasMaxLength(500);
        builder.Property(p => p.MotivoRecusa).HasMaxLength(200);

        builder.OwnsOne(p => p.Valor, v =>
        {
            v.Property(x => x.Valor).HasColumnName("valor").HasColumnType("numeric(12,2)");
        });

        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => p.OsId).IsUnique();
        builder.HasIndex(p => p.PreferenceId);
    }
}
