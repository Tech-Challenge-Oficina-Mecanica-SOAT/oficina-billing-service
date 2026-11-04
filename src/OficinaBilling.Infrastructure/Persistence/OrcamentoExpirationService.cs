using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OficinaBilling.Domain.Enums;

namespace OficinaBilling.Infrastructure.Persistence;

public sealed class OrcamentoExpirationService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OrcamentoExpirationService> _logger;

    public OrcamentoExpirationService(IServiceScopeFactory scopes, ILogger<OrcamentoExpirationService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var expirados = await ExpirarVencidosAsync(stoppingToken);
                if (expirados > 0)
                    _logger.LogInformation("Orçamentos expirados automaticamente: {Quantidade}.", expirados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao expirar orçamentos vencidos.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    public async Task<int> ExpirarVencidosAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        return await db.Orcamentos
            .Where(o => o.Status == StatusOrcamento.Gerado && o.ValidadeAte <= DateTime.UtcNow)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(o => o.Status, StatusOrcamento.Expirado),
                ct);
    }
}
