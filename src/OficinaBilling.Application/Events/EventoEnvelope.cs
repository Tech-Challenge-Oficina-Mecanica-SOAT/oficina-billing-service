using System.Text.Json;
using OficinaBilling.Application.Common;
using OficinaBilling.Domain.Events;

namespace OficinaBilling.Application.Events;

/// <summary>
/// Envelope padrão das mensagens publicadas. O <c>Id</c> permite aos consumidores
/// deduplicar (entrega é at-least-once); o <c>CorrelationId</c> costura o rastreamento.
/// </summary>
public sealed record EventoEnvelope(
    Guid EventId,
    string EventType,
    string EventVersion,
    DateTime OccurredAt,
    string? CorrelationId,
    string Producer,
    JsonElement Data)
{
    public static EventoEnvelope Criar(object evento, string? correlationId)
    {
        var ocorreuEm = DateTime.UtcNow;
        var dados = evento switch
        {
            OrcamentoGeradoEvent e => JsonSerializer.SerializeToElement(new { osId = e.OrdemServicoId, e.OrcamentoId, e.ValorTotal }, JsonDefaults.Options),
            OrcamentoAprovadoEvent e => JsonSerializer.SerializeToElement(new { osId = e.OrdemServicoId, e.OrcamentoId }, JsonDefaults.Options),
            OrcamentoRecusadoEvent e => JsonSerializer.SerializeToElement(new { osId = e.OrdemServicoId, e.OrcamentoId }, JsonDefaults.Options),
            PagamentoConfirmadoEvent e => JsonSerializer.SerializeToElement(new { osId = e.OrdemServicoId, e.PagamentoId }, JsonDefaults.Options),
            PagamentoRecusadoEvent e => JsonSerializer.SerializeToElement(new { osId = e.OrdemServicoId, e.PagamentoId }, JsonDefaults.Options),
            _ => throw new InvalidOperationException($"Evento {evento.GetType().Name} não mapeado para o envelope.")
        };

        return new EventoEnvelope(
            Guid.NewGuid(),
            EventCatalog.TopicoDe(evento),
            "1.0",
            ocorreuEm,
            correlationId,
            "billing-service",
            dados);
    }
}
