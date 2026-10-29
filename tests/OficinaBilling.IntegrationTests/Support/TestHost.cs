using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OficinaBilling.Application.Interfaces;
using Testcontainers.PostgreSql;

namespace OficinaBilling.IntegrationTests.Support;

/// <summary>
/// API real (WebApplicationFactory) + PostgreSQL real (Testcontainers; exige Docker).
/// Só os limites externos são trocados: broker (gravador em memória), Cadastros (fake) e
/// Mercado Pago (FakeMercadoPagoGateway via MercadoPago:UseFake). Compartilhado por todos os testes.
/// </summary>
public sealed class TestHost : WebApplicationFactory<Program>
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static TestHost? _instancia;
    private PostgreSqlContainer _banco = null!;

    public RecordingEventPublisher Eventos { get; } = new();
    public FakeCadastros Cadastros { get; } = new();

    public static async Task<TestHost> ObterAsync()
    {
        await Trava.WaitAsync();
        try
        {
            if (_instancia is null)
            {
                var host = new TestHost();
                await host.IniciarAsync();
                _instancia = host;
            }
            return _instancia;
        }
        finally
        {
            Trava.Release();
        }
    }

    private async Task IniciarAsync()
    {
        _banco = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _banco.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__BillingDb", _banco.GetConnectionString());
        Environment.SetEnvironmentVariable("Messaging__Provider", "Log");
        Environment.SetEnvironmentVariable("Messaging__ConsumerEnabled", "false");
        Environment.SetEnvironmentVariable("Outbox__Enabled", "false"); // os testes disparam o outbox manualmente
        Environment.SetEnvironmentVariable("MercadoPago__UseFake", "true");

        _ = Services; // sobe a aplicação e aplica as migrations versionadas

        AppDomain.CurrentDomain.ProcessExit += (_, _) => _banco.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(Eventos);

            services.RemoveAll<ICadastrosServiceClient>();
            services.AddSingleton<ICadastrosServiceClient>(Cadastros);
        });
    }
}

public sealed class RecordingEventPublisher : IEventPublisher
{
    private readonly ConcurrentQueue<(string Topico, string Payload)> _publicados = new();

    public Task PublicarAsync(string topico, string mensagemJson, IReadOnlyDictionary<string, string> atributos, CancellationToken ct = default)
    {
        _publicados.Enqueue((topico, mensagemJson));
        return Task.CompletedTask;
    }

    /// <summary>Tópicos publicados cujo payload menciona a OS (o banco é compartilhado entre testes).</summary>
    public IReadOnlyList<string> TopicosDa(Guid ordemServicoId) =>
        _publicados.Where(p => p.Payload.Contains(ordemServicoId.ToString())).Select(p => p.Topico).ToList();
}

public sealed class FakeCadastros : ICadastrosServiceClient
{
    private readonly ConcurrentDictionary<Guid, PrecoItem> _itens = new();

    public Guid Registrar(string descricao, decimal preco)
    {
        var id = Guid.NewGuid();
        _itens[id] = new PrecoItem(id, descricao, preco);
        return id;
    }

    public Task<PrecoItem?> ObterPrecoPecaAsync(Guid pecaId, CancellationToken ct = default) =>
        Task.FromResult(_itens.GetValueOrDefault(pecaId));

    public Task<PrecoItem?> ObterPrecoServicoAsync(Guid servicoId, CancellationToken ct = default) =>
        Task.FromResult(_itens.GetValueOrDefault(servicoId));
}
