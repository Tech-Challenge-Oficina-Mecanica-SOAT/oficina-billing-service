namespace OficinaBilling.Application.Interfaces;

// Porta para publicar Domain Events como mensagens SNS, através do Outbox.
// A Infrastructure decide o eventType/topic de cada tipo de evento e monta
// o envelope padrão (eventId/eventType/eventVersion/occurredAt/correlationId/producer/data).
public interface IEventPublisher
{
    Task EnfileirarAsync<TEvento>(TEvento evento, CancellationToken ct = default) where TEvento : notnull;
}
