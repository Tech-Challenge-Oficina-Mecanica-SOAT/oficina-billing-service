namespace OficinaBilling.Application.Interfaces;

// Porta para publicar Domain Events como mensagens SNS.
// A Infrastructure decide o nome do topic (via Parameter Store) para cada tipo de evento.
public interface IEventPublisher
{
    Task PublicarAsync<TEvento>(TEvento evento, CancellationToken ct = default) where TEvento : notnull;
}
