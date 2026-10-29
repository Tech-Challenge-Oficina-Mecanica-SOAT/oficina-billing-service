namespace OficinaBilling.Domain.Events;

public sealed record PagamentoRecusadoEvent(Guid OsId, Guid PagamentoId, string Motivo, DateTime RecusadoEm);
