using OficinaBilling.Application.Common;

namespace OficinaBilling.Infrastructure.Http;

/// <summary>Propaga o X-Correlation-Id nas chamadas REST de saída (Billing → Cadastros/OS).</summary>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    public const string Header = "X-Correlation-Id";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (CorrelationContext.Atual is { } id && !request.Headers.Contains(Header))
            request.Headers.Add(Header, id);

        return base.SendAsync(request, ct);
    }
}
