namespace OficinaBilling.Application.Common;

/// <summary>
/// Correlation id do fluxo corrente (request HTTP ou mensagem SQS). Viaja em logs,
/// chamadas REST e eventos publicados, permitindo rastrear uma OS entre os serviços.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> _atual = new();

    public static string? Atual => _atual.Value;

    public static IDisposable Definir(string? correlationId)
    {
        var anterior = _atual.Value;
        _atual.Value = correlationId;
        return new Restaurador(anterior);
    }

    private sealed class Restaurador(string? anterior) : IDisposable
    {
        public void Dispose() => _atual.Value = anterior;
    }
}
