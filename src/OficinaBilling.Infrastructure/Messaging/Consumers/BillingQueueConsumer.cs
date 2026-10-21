using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OficinaBilling.Infrastructure.Messaging.Consumers;

// Long-polling ÚNICO na fila oficina-{env}-billing-queue, que recebe todos os eventos que o
// Billing consome (diagnostico.concluido, execucao.finalizada, os.cancelada) — a assinatura
// SNS->SQS de cada topic é quem decide o que cai aqui (ver terraform/subscriptions.tf).
// Um único consumer despachando por eventType evita vários pollers brigando pela fila.
//
// Pressupõe "raw message delivery" habilitado nas subscriptions, então o corpo da mensagem
// SQS já é o envelope puro (sem o wrapper de notificação do SNS).
public class BillingQueueConsumer : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly string _queueUrl;
    private readonly BillingEventDispatcher _dispatcher;
    private readonly ILogger<BillingQueueConsumer> _logger;

    public BillingQueueConsumer(IAmazonSQS sqs, string queueUrl, BillingEventDispatcher dispatcher, ILogger<BillingQueueConsumer> logger)
    {
        _sqs = sqs;
        _queueUrl = queueUrl;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse response;
            try
            {
                response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 15, // long polling
                    VisibilityTimeout = 30
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao consultar a fila SQS {QueueUrl}.", _queueUrl);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            // Atenção: no AWS SDK for .NET v4, coleções vazias vêm como NULL (não como lista vazia).
            // É o caso normal de um long polling que expirou sem mensagens, e iterar direto
            // derrubava o BackgroundService (e, com o padrão do .NET, o host inteiro).
            if (response.Messages is null || response.Messages.Count == 0)
                continue;

            foreach (var message in response.Messages)
                await ProcessarMensagemAsync(message, stoppingToken);
        }
    }

    private async Task ProcessarMensagemAsync(Message message, CancellationToken ct)
    {
        try
        {
            await _dispatcher.ProcessarAsync(message.Body, message.MessageId, ct);

            // Só apaga depois do processamento ter sucesso. Se o dispatcher lançar exceção (inclusive
            // falha de negócio), a mensagem volta a ficar visível após o VisibilityTimeout e é
            // reentregue; depois de N tentativas (redrive_policy) cai na DLQ.
            await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao processar mensagem SQS {MessageId}; será reentregue (ou irá para a DLQ).", message.MessageId);
        }
    }
}
