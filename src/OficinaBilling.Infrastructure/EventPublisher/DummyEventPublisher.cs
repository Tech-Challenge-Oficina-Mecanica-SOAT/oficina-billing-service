using Microsoft.Extensions.Logging;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.EventPublisher;

public class DummyEventPublisher : IEventPublisher
{
    private readonly ILogger<DummyEventPublisher> _logger;

    public DummyEventPublisher(ILogger<DummyEventPublisher> logger)
    {
        _logger = logger;
    }

    Task IEventPublisher.PublicarAsync<TEvento>(TEvento @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Evento publicado: {EventType}", typeof(TEvento).Name);
        
        // Aqui você pode adicionar lógica para salvar em um arquivo ou banco
        // para depuração, se necessário
        
        return Task.CompletedTask;
    }
}