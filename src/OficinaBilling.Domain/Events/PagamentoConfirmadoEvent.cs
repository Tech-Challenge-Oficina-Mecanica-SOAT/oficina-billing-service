namespace OficinaBilling.Domain.Events;

public sealed record PagamentoConfirmadoEvent(Guid OsId, Guid PagamentoId, decimal Valor, DateTime ConfirmadoEm);
