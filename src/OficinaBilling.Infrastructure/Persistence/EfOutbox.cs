using System.Text.Json;
using OficinaBilling.Application.Common;
using OficinaBilling.Application.Events;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Infrastructure.Persistence.Entities;

namespace OficinaBilling.Infrastructure.Persistence;

/// <summary>
/// Só adiciona a linha ao DbContext; ela é gravada pelo mesmo SaveChanges do agregado (mesma transação).
/// </summary>
public sealed class EfOutbox : IOutbox
{
    private readonly BillingDbContext _context;

    public EfOutbox(BillingDbContext context) => _context = context;

    public void Enfileirar(object evento)
    {
        var envelope = EventoEnvelope.Criar(evento, CorrelationContext.Atual);

        _context.OutboxMessages.Add(new OutboxMessage
        {
            Id = envelope.EventId,
            Topico = envelope.EventType,
            Payload = JsonSerializer.Serialize(envelope, JsonDefaults.Options),
            CorrelationId = envelope.CorrelationId,
            OcorridoEm = envelope.OccurredAt
        });
    }
}
