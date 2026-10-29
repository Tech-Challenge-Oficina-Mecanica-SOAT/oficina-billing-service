using OficinaBilling.Application.Common;
using OficinaBilling.Infrastructure.Http;

namespace OficinaBilling.API.Middleware;

/// <summary>
/// Lê (ou gera) o X-Correlation-Id, devolve no response, coloca no escopo de log e no
/// CorrelationContext (que alimenta chamadas REST de saída e o envelope dos eventos).
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[CorrelationIdHandler.Header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id))
            id = context.TraceIdentifier;

        context.Response.Headers[CorrelationIdHandler.Header] = id;

        using (CorrelationContext.Definir(id))
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await _next(context);
        }
    }
}
