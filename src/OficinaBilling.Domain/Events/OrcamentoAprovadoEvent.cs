namespace OficinaBilling.Domain.Events;

public sealed record OrcamentoAprovadoEvent(Guid OsId, Guid OrcamentoId, DateTime AprovadoEm);
