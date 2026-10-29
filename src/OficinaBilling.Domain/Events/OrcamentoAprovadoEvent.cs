namespace OficinaBilling.Domain.Events;

public sealed record OrcamentoAprovadoEvent(Guid OrcamentoId, Guid OrdemServicoId);
