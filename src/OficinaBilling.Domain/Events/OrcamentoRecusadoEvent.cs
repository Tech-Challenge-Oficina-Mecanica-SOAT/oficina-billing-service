namespace OficinaBilling.Domain.Events;

public sealed record OrcamentoRecusadoEvent(Guid OsId, Guid OrcamentoId, string? Motivo, DateTime RecusadoEm);
