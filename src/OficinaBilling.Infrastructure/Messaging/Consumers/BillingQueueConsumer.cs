using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.UseCases.Orcamento;
using OficinaBilling.Application.UseCases.Pagamento;
using OficinaBilling.Domain.Interfaces;
using System.Text.Json;

namespace OficinaBilling.Infrastructure.Messaging.Consumers;

// Long-polling ÚNICO na fila oficina-{env}-billing-queue, que recebe todos os
// eventos que o Billing consome (diagnostico.concluido, execucao.finalizada,
// os.cancelada) — a assinatura SNS->SQS de cada topic é quem decide o que cai
// aqui (ver terraform/subscriptions.tf). Um único consumer despachando por
// eventType evita vários pollers brigando pela mesma fila.
//
// Pressupõe "raw message delivery" habilitado nas subscriptions, então o
// corpo da mensagem SQS já é o envelope puro (sem o wrapper de notificação do SNS).
public class BillingQueueConsumer : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly string _queueUrl;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingQueueConsumer> _logger;

    public BillingQueueConsumer(IAmazonSQS sqs, string queueUrl, IServiceScopeFactory scopeFactory, ILogger<BillingQueueConsumer> logger)
    {
        _sqs = sqs;
        _queueUrl = queueUrl;
        _scopeFactory = scopeFactory;
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao consultar a fila SQS {QueueUrl}.", _queueUrl);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            foreach (var message in response.Messages)
                await ProcessarMensagemAsync(message, stoppingToken);
        }
    }

    private async Task ProcessarMensagemAsync(Message message, CancellationToken ct)
    {
        try
        {
            using var envelope = JsonDocument.Parse(message.Body);
            var root = envelope.RootElement;
            var eventType = root.GetProperty("eventType").GetString();
            var eventId = root.GetProperty("eventId").GetString() ?? message.MessageId;
            var data = root.GetProperty("data");

            using var scope = _scopeFactory.CreateScope();

            // Idempotência de infraestrutura: eventId já visto -> mensagem duplicada, só apaga.
            var idempotencia = scope.ServiceProvider.GetRequiredService<IIdempotenciaRepository>();
            if (await idempotencia.JaProcessadoAsync(eventId, ct))
            {
                _logger.LogInformation("Evento {EventId} ({EventType}) já processado, ignorando.", eventId, eventType);
                await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, ct);
                return;
            }

            switch (eventType)
            {
                case "diagnostico.concluido":
                    await TratarDiagnosticoConcluidoAsync(scope.ServiceProvider, data, ct);
                    break;
                case "execucao.finalizada":
                    await TratarExecucaoFinalizadaAsync(scope.ServiceProvider, data, ct);
                    break;
                case "os.cancelada":
                    await TratarOsCanceladaAsync(scope.ServiceProvider, data, ct);
                    break;
                default:
                    _logger.LogWarning("Evento {EventType} recebido mas não é consumido pelo Billing — ignorando.", eventType);
                    break;
            }

            await idempotencia.MarcarProcessadoAsync(eventId, ct);

            // Só apaga a mensagem depois do processamento ter sucesso. Se der exceção antes
            // disto, a mensagem volta a ficar visível após o VisibilityTimeout e é reentregue;
            // depois de N tentativas (redrive_policy) cai na DLQ.
            await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar mensagem SQS {MessageId}.", message.MessageId);
        }
    }

    // data = payload de "diagnostico.concluido" (contratos/eventos/schemas.md, evento #3)
    private static async Task TratarDiagnosticoConcluidoAsync(IServiceProvider sp, JsonElement data, CancellationToken ct)
    {
        var useCase = sp.GetRequiredService<GerarOrcamentoUseCase>();

        var request = new GerarOrcamentoRequest(
            OsId: data.GetProperty("osId").GetGuid(),
            DiagnosticoId: data.GetProperty("diagnosticoId").GetGuid(),
            Pecas: data.GetProperty("pecas").EnumerateArray()
                .Select(p => new ItemDiagnostico(p.GetProperty("pecaId").GetGuid(), p.GetProperty("quantidade").GetInt32()))
                .ToList(),
            Servicos: data.GetProperty("servicos").EnumerateArray()
                .Select(s => new ItemDiagnostico(s.GetProperty("servicoId").GetGuid(), s.GetProperty("quantidade").GetInt32()))
                .ToList());

        await useCase.ExecutarAsync(request, ct);
    }

    // data = payload de "execucao.finalizada" (evento #8)
    private static async Task TratarExecucaoFinalizadaAsync(IServiceProvider sp, JsonElement data, CancellationToken ct)
    {
        var useCase = sp.GetRequiredService<CriarCobrancaUseCase>();
        var osId = data.GetProperty("osId").GetGuid();
        await useCase.ExecutarAsync(osId, ct);
    }

    // data = payload de "os.cancelada" (evento #2) — compensação da saga
    private static async Task TratarOsCanceladaAsync(IServiceProvider sp, JsonElement data, CancellationToken ct)
    {
        var useCase = sp.GetRequiredService<CancelarOrcamentoPorOsUseCase>();
        var osId = data.GetProperty("osId").GetGuid();
        await useCase.ExecutarAsync(osId, ct);
    }
}
