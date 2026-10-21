using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Messaging;

// Lê periodicamente a tabela Outbox e publica no SNS o que ainda não foi
// publicado. Roda em background — é o que efetivamente fecha o Outbox Pattern.
// Usa IServiceScopeFactory porque BackgroundService é singleton e os
// repositórios/DbContext são scoped.
public class OutboxPublisherBackgroundService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly TopicArnResolver _topicArnResolver;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        IAmazonSimpleNotificationService sns,
        TopicArnResolver topicArnResolver,
        ILogger<OutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _sns = sns;
        _topicArnResolver = topicArnResolver;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublicarPendentesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no ciclo do OutboxPublisherBackgroundService.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task PublicarPendentesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var pendentes = await outboxRepository.ObterPendentesAsync(ct: ct);
        foreach (var mensagem in pendentes)
        {
            try
            {
                var topicArn = await _topicArnResolver.ResolverAsync(mensagem.EventType, ct);
                await _sns.PublishAsync(new PublishRequest { TopicArn = topicArn, Message = mensagem.Payload }, ct);
                await outboxRepository.MarcarPublicadaAsync(mensagem.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao publicar evento {EventId} ({EventType}) — tentará de novo no próximo ciclo.",
                    mensagem.Id, mensagem.EventType);
                await outboxRepository.RegistrarFalhaAsync(mensagem.Id, ex.Message, ct);
            }
        }
    }
}
