using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using System.Collections.Concurrent;

namespace OficinaBilling.Infrastructure.Messaging;

// Resolve eventType -> ARN do topic via Parameter Store, com cache em memória
// (o ARN não muda em runtime; evita bater no SSM a cada publicação).
// Caminho: /oficina/{env}/messaging/topics/{eventType}/arn — ver contratos/eventos/schemas.md.
public class TopicArnResolver
{
    private readonly IAmazonSimpleSystemsManagement _ssm;
    private readonly string _environment;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public TopicArnResolver(IAmazonSimpleSystemsManagement ssm, string environment)
    {
        _ssm = ssm;
        _environment = environment;
    }

    public async Task<string> ResolverAsync(string eventType, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(eventType, out var cached))
            return cached;

        var parametro = await _ssm.GetParameterAsync(new GetParameterRequest
        {
            Name = $"/oficina/{_environment}/messaging/topics/{eventType}/arn"
        }, ct);

        var arn = parametro.Parameter.Value;
        _cache[eventType] = arn;
        return arn;
    }
}
