using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OficinaBilling.Application.Common;
using OficinaBilling.Application.Messaging;
using OficinaBilling.Infrastructure.Configuration;

namespace OficinaBilling.Infrastructure.Messaging.Aws;

/// <summary>
/// Consumidor da fila oficina-{env}-billing-queue (long polling).
///  - Sucesso  -> apaga a mensagem.
///  - Falha/exceção -> NÃO apaga: volta após o visibility timeout e, depois de maxReceiveCount
///    tentativas, o SQS a move para a DLQ (retry nativo, sem código extra).
/// </summary>
public sealed class SqsConsumerService : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly IServiceScopeFactory _scopes;
    private readonly MessagingOptions _options;
    private readonly ILogger<SqsConsumerService> _logger;

    public SqsConsumerService(IAmazonSQS sqs, IServiceScopeFactory scopes, IOptions<MessagingOptions> options, ILogger<SqsConsumerService> logger)
    {
        _sqs = sqs;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ConsumerEnabled)
        {
            _logger.LogInformation("Consumidor SQS desabilitado.");
            return;
        }

        string? queueUrl = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                queueUrl ??= await ResolverQueueUrlAsync(stoppingToken);
                _logger.LogInformation("Consumindo a fila {QueueUrl}", queueUrl);

                var resposta = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = _options.MaxMensagensPorLeitura,
                    WaitTimeSeconds = 20,
                    MessageAttributeNames = new List<string> { "All" }
                }, stoppingToken);

                foreach (var mensagem in resposta.Messages ?? new List<Message>())
                {
                    if (await ProcessarAsync(mensagem, stoppingToken))
                        await _sqs.DeleteMessageAsync(queueUrl, mensagem.ReceiptHandle, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ex is QueueDoesNotExistException)
                    queueUrl = null;

                _logger.LogError(ex, "Erro ao ler a fila SQS. Nova tentativa em 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessarAsync(Message mensagem, CancellationToken ct)
    {
        try
        {
            var entrante = MensagemEntrante.Parse(mensagem.Body);

            using var _ = CorrelationContext.Definir(entrante.CorrelationId ?? mensagem.MessageId);
            using var scope = _scopes.CreateScope();

            var handler = scope.ServiceProvider.GetServices<IMensagemHandler>()
                .FirstOrDefault(h => h.Topico == entrante.Topico);

            if (handler is null)
            {
                _logger.LogWarning("Mensagem {Id} com tópico sem handler ('{Topico}'): descartada.", mensagem.MessageId, entrante.Topico);
                return true;
            }

            var resultado = await handler.ProcessarAsync(entrante.PayloadJson, ct);
            if (resultado.Sucesso)
                return true;

            _logger.LogWarning("Mensagem {Id} ({Topico}) não processada: {Erro}. Será reentregue/DLQ.",
                mensagem.MessageId, entrante.Topico, resultado.Erro);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Erro ao processar a mensagem {Id}. Será reentregue/DLQ.", mensagem.MessageId);
            return false;
        }
    }

    private async Task<string> ResolverQueueUrlAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_options.QueueUrl))
            return _options.QueueUrl;

        var nome = $"oficina-{_options.Environment}-billing-queue";
        return (await _sqs.GetQueueUrlAsync(nome, ct)).QueueUrl;
    }
}
