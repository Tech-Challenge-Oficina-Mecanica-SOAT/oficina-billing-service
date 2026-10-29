using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Infrastructure.Configuration;
using OficinaBilling.Infrastructure.Persistence;

namespace OficinaBilling.Infrastructure.Messaging;

/// <summary>
/// Dispatcher do Outbox Pattern: lê mensagens pendentes e publica no broker.
/// FOR UPDATE SKIP LOCKED permite várias réplicas do pod sem publicar duas vezes a mesma linha.
/// Garantia: at-least-once (consumidores deduplicam pelo Id do envelope).
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopes, IOptions<OutboxOptions> options, ILogger<OutboxProcessor> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Outbox dispatcher desabilitado.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var publicadas = 0;
            try
            {
                publicadas = await ProcessarLoteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar o outbox.");
            }

            if (publicadas == 0)
                await Task.Delay(TimeSpan.FromSeconds(_options.IntervaloSegundos), stoppingToken);
        }
    }

    /// <summary>Processa um lote e devolve quantas mensagens foram publicadas. Público para testes.</summary>
    public async Task<int> ProcessarLoteAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        await using var transacao = await context.Database.BeginTransactionAsync(ct);

        var lote = _options.TamanhoLote;
        var max = _options.MaxTentativas;
        var mensagens = await context.OutboxMessages
            .FromSqlInterpolated($@"SELECT * FROM outbox_messages
                                    WHERE ""ProcessadoEm"" IS NULL AND ""Tentativas"" < {max}
                                    ORDER BY ""OcorridoEm""
                                    LIMIT {lote}
                                    FOR UPDATE SKIP LOCKED")
            .ToListAsync(ct);

        var publicadas = 0;
        foreach (var mensagem in mensagens)
        {
            try
            {
                var atributos = new Dictionary<string, string> { ["eventType"] = mensagem.Topico, ["eventId"] = mensagem.Id.ToString() };
                if (mensagem.CorrelationId is not null)
                    atributos["correlationId"] = mensagem.CorrelationId;

                await publisher.PublicarAsync(mensagem.Topico, mensagem.Payload, atributos, ct);
                mensagem.ProcessadoEm = DateTime.UtcNow;
                publicadas++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mensagem.Tentativas++;
                mensagem.UltimoErro = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                if (mensagem.Tentativas >= max)
                {
                    _logger.LogCritical(ex,
                        "Outbox {Id} ({Topico}) atingiu o limite de tentativas ({Max}) e requer intervenção/reprocessamento manual.",
                        mensagem.Id, mensagem.Topico, max);
                }
                else
                {
                    _logger.LogError(ex, "Falha ao publicar {Topico} (outbox {Id}, tentativa {Tentativa}/{Max}).",
                        mensagem.Topico, mensagem.Id, mensagem.Tentativas, max);
                }
            }
        }

        await context.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return publicadas;
    }
}
