using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Infrastructure.Persistence.Configurations;

public class OrcamentoConfiguration : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> builder)
    {
        builder.ToTable("orcamentos");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OsId).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.MotivoRecusa).HasMaxLength(500);

        // Dinheiro é Value Object -> mapeado como coluna simples (owned).
        builder.OwnsOne(o => o.ValorTotal, v =>
        {
            v.Property(x => x.Valor).HasColumnName("valor_total").HasColumnType("numeric(12,2)");
        });

        // Itens são um Value Collection -> tabela filha própria, sem entidade agregada distinta.
        builder.OwnsMany(o => o.Itens, item =>
        {
            item.ToTable("orcamento_itens");
            item.WithOwner().HasForeignKey("OrcamentoId");
            item.HasKey(i => i.Id);
            item.Property(i => i.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
            item.Property(i => i.ReferenciaId).IsRequired();
            item.Property(i => i.Nome).HasMaxLength(200).IsRequired();
            item.Property(i => i.Quantidade).IsRequired();
            item.Property(i => i.PrecoUnitario).HasColumnType("numeric(12,2)").IsRequired();
        });

        // Domain events não são persistidos — EF ignora a coleção in-memory.
        builder.Ignore(o => o.DomainEvents);

        builder.HasIndex(o => o.OsId).IsUnique();
    }
}
