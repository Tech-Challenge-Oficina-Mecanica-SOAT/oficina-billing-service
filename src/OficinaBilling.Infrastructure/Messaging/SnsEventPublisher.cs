using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Events;
using System.Text.Json;

namespace OficinaBilling.Infrastructure.Messaging;

// Publica cada Domain Event no SNS topic correspondente. O ARN do topic
// é lido do Parameter Store (SSM) em vez de hardcoded — o Terraform deste
// próprio repositório é quem cria o topic e escreve o parâmetro
// (ver terraform/messaging.tf). Nenhum outro repositório precisa saber disso.
public class SnsEventPublisher : IEventPublisher
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonSimpleSystemsManagement _ssm;
    private readonly string _environment;

    public SnsEventPublisher(IAmazonSimpleNotificationService sns, IAmazonSimpleSystemsManagement ssm, string environment)
    {
        _sns = sns;
        _ssm = ssm;
        _environment = environment;
    }

    public async Task PublicarAsync<TEvento>(TEvento evento, CancellationToken ct = default) where TEvento : notnull
    {
        var nomeTopic = MapearNomeTopic(evento);
        var parametro = await _ssm.GetParameterAsync(new GetParameterRequest
        {
            Name = $"/oficina/{_environment}/messaging/topics/{nomeTopic}/arn"
        }, ct);

        await _sns.PublishAsync(new PublishRequest
        {
            TopicArn = parametro.Parameter.Value,
            Message = JsonSerializer.Serialize(evento)
        }, ct);
    }

    private static string MapearNomeTopic<TEvento>(TEvento evento) => evento switch
    {
        OrcamentoGeradoEvent => "orcamento.gerado",
        OrcamentoAprovadoEvent => "orcamento.aprovado",
        OrcamentoRecusadoEvent => "orcamento.recusado",
        PagamentoConfirmadoEvent => "pagamento.confirmado",
        PagamentoRecusadoEvent => "pagamento.recusado",
        _ => throw new InvalidOperationException($"Evento {typeof(TEvento).Name} não mapeado para nenhum topic.")
    };
}
