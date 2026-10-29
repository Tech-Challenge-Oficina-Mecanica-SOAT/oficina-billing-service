namespace OficinaBilling.Domain.Events;

public sealed record PagamentoConfirmadoEvent(Guid PagamentoId, Guid OrdemServicoId);
