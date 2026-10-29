using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.UseCases.Orcamento;
using OficinaBilling.Application.UseCases.Pagamento;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Messaging.Consumers;

// Lógica de processamento de UMA mensagem (envelope JSON), separada do polling do SQS
// para poder ser testada sem AWS. Contrato:
//   - retorna normalmente  -> processado (ou duplicado): o consumer pode apagar a mensagem;
//   - lança exceção        -> NÃO apagar: o SQS reentrega após o VisibilityTimeout e, depois de
//                             maxReceiveCount tentativas, manda para a DLQ (redrive_policy).
public class BillingEventDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingEventDispatcher> _logger;

    public BillingEventDispatcher(IServiceScopeFactory scopeFactory, ILogger<BillingEventDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ProcessarAsync(string body, string messageIdFallback, CancellationToken ct)
    {
        using var envelope = JsonDocument.Parse(body);
        var root = envelope.RootElement;
        var eventType = root.GetProperty("eventType").GetString();
        var eventId = root.GetProperty("eventId").GetString() ?? messageIdFallback;
        var data = root.GetProperty("data");

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        // Idempotência de infraestrutura: eventId já visto -> mensagem duplicada.
        var idempotencia = sp.GetRequiredService<IIdempotenciaRepository>();
        if (await idempotencia.JaProcessadoAsync(eventId, ct))
        {
            _logger.LogInformation("Evento {EventId} ({EventType}) já processado, ignorando.", eventId, eventType);
            return;
        }

        switch (eventType)
        {
            case "diagnostico.concluido":
                await TratarDiagnosticoConcluidoAsync(sp, data, ct);
                break;
            case "execucao.finalizada":
                await TratarExecucaoFinalizadaAsync(sp, data, ct);
                break;
            case "os.cancelada":
                await TratarOsCanceladaAsync(sp, data, ct);
                break;
            default:
                _logger.LogWarning("Evento {EventType} recebido mas não é consumido pelo Billing — ignorando.", eventType);
                break;
        }

        await idempotencia.MarcarProcessadoAsync(eventId, ct);
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

        var resultado = await useCase.ExecutarAsync(request, ct);
        if (!resultado.Sucesso)
            throw new InvalidOperationException($"Falha ao gerar orçamento da OS {request.OsId}: {resultado.Erro}");
    }

    // data = payload de "execucao.finalizada" (evento #8)
    private static async Task TratarExecucaoFinalizadaAsync(IServiceProvider sp, JsonElement data, CancellationToken ct)
    {
        var useCase = sp.GetRequiredService<CriarCobrancaUseCase>();
        var osId = data.GetProperty("osId").GetGuid();

        var resultado = await useCase.ExecutarAsync(osId, ct);
        if (!resultado.Sucesso)
            throw new InvalidOperationException($"Falha ao criar cobrança da OS {osId}: {resultado.Erro}");
    }

    // data = payload de "os.cancelada" (evento #2): compensação da saga
    private static async Task TratarOsCanceladaAsync(IServiceProvider sp, JsonElement data, CancellationToken ct)
    {
        var useCase = sp.GetRequiredService<CancelarOrcamentoPorOsUseCase>();
        var osId = data.GetProperty("osId").GetGuid();
        await useCase.ExecutarAsync(osId, ct);
    }
}
