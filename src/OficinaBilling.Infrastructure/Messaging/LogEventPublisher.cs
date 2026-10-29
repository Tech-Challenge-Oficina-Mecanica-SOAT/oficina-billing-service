using Microsoft.Extensions.Logging;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.Messaging;

/// <summary>Publisher de dev local (Messaging:Provider=Log): só registra no log, não toca em AWS.</summary>
public sealed class LogEventPublisher : IEventPublisher
{
    private readonly ILogger<LogEventPublisher> _logger;

    public LogEventPublisher(ILogger<LogEventPublisher> logger) => _logger = logger;

    public Task PublicarAsync(string topico, string mensagemJson, IReadOnlyDictionary<string, string> atributos, CancellationToken ct = default)
    {
        _logger.LogInformation("[evento] {Topico}: {Mensagem}", topico, mensagemJson);
        return Task.CompletedTask;
    }
}
