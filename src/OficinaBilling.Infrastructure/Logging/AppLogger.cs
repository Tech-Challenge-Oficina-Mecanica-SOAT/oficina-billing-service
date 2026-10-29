using Microsoft.Extensions.Logging;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.Logging;

// Adapter fino: a Application depende só de IAppLogger<T> (porta), não do
// Microsoft.Extensions.Logging diretamente — mantém a camada livre de framework.
public class AppLogger<T> : IAppLogger<T>
{
    private readonly ILogger<T> _logger;

    public AppLogger(ILogger<T> logger) => _logger = logger;

    public void LogInformation(string mensagem, params object[] args) => _logger.LogInformation(mensagem, args);
    public void LogWarning(string mensagem, params object[] args) => _logger.LogWarning(mensagem, args);
    public void LogError(Exception ex, string mensagem, params object[] args) => _logger.LogError(ex, mensagem, args);
}
