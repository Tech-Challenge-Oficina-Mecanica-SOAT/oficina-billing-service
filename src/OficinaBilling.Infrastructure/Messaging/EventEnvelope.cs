namespace OficinaBilling.Infrastructure.Messaging;

// Envelope padrão descrito em contratos/eventos/schemas.md.
public sealed record EventEnvelope(
    Guid EventId,
    string EventType,
    string EventVersion,
    DateTime OccurredAt,
    string? CorrelationId,
    string Producer,
    object Data);
