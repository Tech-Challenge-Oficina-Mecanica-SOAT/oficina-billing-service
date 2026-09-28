namespace OficinaBilling.Domain.Events;

public sealed record OrcamentoRecusadoEvent(Guid OrcamentoId, Guid OrdemServicoId);
