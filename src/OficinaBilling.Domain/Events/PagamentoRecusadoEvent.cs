namespace OficinaBilling.Domain.Events;

public sealed record PagamentoRecusadoEvent(Guid PagamentoId, Guid OrdemServicoId);
