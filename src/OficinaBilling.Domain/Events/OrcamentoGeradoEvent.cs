namespace OficinaBilling.Domain.Events;

// Domain Event interno. Vira mensagem SNS ("orcamento.gerado") na camada de
// Infrastructure — o Domain não sabe que SNS existe.
public sealed record OrcamentoGeradoEvent(Guid OrcamentoId, Guid OrdemServicoId, decimal ValorTotal);
