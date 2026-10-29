using System.Collections.Concurrent;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Options;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Infrastructure.Configuration;

namespace OficinaBilling.Infrastructure.Messaging.Aws;

// Publica a mensagem do outbox no SNS topic correspondente. O ARN do topic é lido do
// Parameter Store (criado pelo Terraform deste repositório — ver terraform/messaging.tf)
// e cacheado em memória.
public sealed class SnsEventPublisher : IEventPublisher
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonSimpleSystemsManagement _ssm;
    private readonly string _environment;
    private readonly ConcurrentDictionary<string, string> _arns = new();

    public SnsEventPublisher(
        IAmazonSimpleNotificationService sns,
        IAmazonSimpleSystemsManagement ssm,
        IOptions<MessagingOptions> options)
    {
        _sns = sns;
        _ssm = ssm;
        _environment = options.Value.Environment;
    }

    public async Task PublicarAsync(string topico, string mensagemJson, IReadOnlyDictionary<string, string> atributos, CancellationToken ct = default)
    {
        var arn = await ResolverArnAsync(topico, ct);

        var request = new PublishRequest
        {
            TopicArn = arn,
            Message = mensagemJson,
            // AWSSDK v4 não inicializa coleções: atribuímos explicitamente.
            MessageAttributes = atributos.ToDictionary(
                a => a.Key,
                a => new MessageAttributeValue { DataType = "String", StringValue = a.Value })
        };

        await _sns.PublishAsync(request, ct);
    }

    private async Task<string> ResolverArnAsync(string topico, CancellationToken ct)
    {
        if (_arns.TryGetValue(topico, out var cacheado))
            return cacheado;

        var parametro = await _ssm.GetParameterAsync(new GetParameterRequest
        {
            Name = $"/oficina/{_environment}/messaging/topics/{topico}/arn"
        }, ct);

        return _arns[topico] = parametro.Parameter.Value;
    }
}
