namespace OficinaBilling.Domain.Events;

// Payload interno do Domain Event. A Infrastructure envolve isto no envelope
// padrão (eventId/eventType/eventVersion/...) antes de publicar no SNS
// (ver contratos/eventos/schemas.md) e usa "orcamento.gerado" como eventType.
public sealed record OrcamentoGeradoEvent(Guid OsId, Guid OrcamentoId, decimal ValorTotal, DateTime ValidadeAte, DateTime GeradoEm);
