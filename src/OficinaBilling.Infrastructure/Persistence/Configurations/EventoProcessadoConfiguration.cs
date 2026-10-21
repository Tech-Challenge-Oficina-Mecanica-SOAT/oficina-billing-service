using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Infrastructure.Persistence.Configurations;

public class EventoProcessadoConfiguration : IEntityTypeConfiguration<EventoProcessado>
{
    public void Configure(EntityTypeBuilder<EventoProcessado> builder)
    {
        builder.ToTable("eventos_processados");
        builder.HasKey(e => e.Chave);
        builder.Property(e => e.Chave).HasMaxLength(200);
        // TODO: job periódico para apagar linhas com ProcessadoEm < now-7d (retenção do contrato)
    }
}
