using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.UnitTests.Fakes;

// Fakes em memória (em vez de Moq) para os testes que precisam de ESTADO ao longo de vários
// passos — ex.: webhook chegando duas vezes, recusa seguida de aprovação.

public class InMemoryOrcamentoRepository : IOrcamentoRepository
{
    public List<Orcamento> Itens { get; } = new();
    public Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Itens.FirstOrDefault(o => o.Id == id));
    public Task<Orcamento?> ObterPorOsIdAsync(Guid osId, CancellationToken ct = default) => Task.FromResult(Itens.FirstOrDefault(o => o.OsId == osId));
    public Task AdicionarAsync(Orcamento orcamento, CancellationToken ct = default) { Itens.Add(orcamento); return Task.CompletedTask; }
}

public class InMemoryPagamentoRepository : IPagamentoRepository
{
    public List<Pagamento> Itens { get; } = new();
    public Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Itens.FirstOrDefault(p => p.Id == id));
    public Task<Pagamento?> ObterPorOsIdAsync(Guid osId, CancellationToken ct = default) => Task.FromResult(Itens.FirstOrDefault(p => p.OsId == osId));
    public Task<Pagamento?> ObterPorPreferenceIdAsync(string preferenceId, CancellationToken ct = default) => Task.FromResult(Itens.FirstOrDefault(p => p.PreferenceId == preferenceId));
    public Task<Pagamento?> ObterPorOrcamentoIdAsync(Guid orcamentoId, CancellationToken ct = default) => Task.FromResult(Itens.FirstOrDefault(p => p.OrcamentoId == orcamentoId));
    public Task AdicionarAsync(Pagamento pagamento, CancellationToken ct = default) { Itens.Add(pagamento); return Task.CompletedTask; }
}

public class InMemoryIdempotenciaRepository : IIdempotenciaRepository
{
    public HashSet<string> Chaves { get; } = new();
    public Task<bool> JaProcessadoAsync(string chave, CancellationToken ct = default) => Task.FromResult(Chaves.Contains(chave));
    public Task MarcarProcessadoAsync(string chave, CancellationToken ct = default) { Chaves.Add(chave); return Task.CompletedTask; }
}

public class FakeUnitOfWork : IUnitOfWork
{
    public int Salvamentos { get; private set; }
    public Task SalvarAlteracoesAsync(CancellationToken ct = default) { Salvamentos++; return Task.CompletedTask; }
}

public class FakeEventPublisher : IEventPublisher
{
    public List<object> Eventos { get; } = new();
    public Task EnfileirarAsync<TEvento>(TEvento evento, CancellationToken ct = default) where TEvento : notnull
    {
        Eventos.Add(evento);
        return Task.CompletedTask;
    }
}

public class FakeMercadoPagoGateway : IMercadoPagoGateway
{
    public Dictionary<string, StatusPagamentoMp> Pagamentos { get; } = new();
    public List<(decimal Valor, Guid ReferenciaExterna)> PreferenciasCriadas { get; } = new();

    public Task<PreferenciaCriada> CriarPreferenciaAsync(decimal valor, string descricao, Guid referenciaExterna, CancellationToken ct = default)
    {
        PreferenciasCriadas.Add((valor, referenciaExterna));
        return Task.FromResult(new PreferenciaCriada($"pref-{PreferenciasCriadas.Count}", $"https://mp.test/checkout/pref-{PreferenciasCriadas.Count}"));
    }

    public Task<StatusPagamentoMp?> ConsultarPagamentoAsync(string mercadoPagoPaymentId, CancellationToken ct = default) =>
        Task.FromResult(Pagamentos.TryGetValue(mercadoPagoPaymentId, out var p) ? p : null);

    // Atalho para o teste "programar" o que o MP responderia.
    public void Define(string paymentId, string status, decimal valor, string? externalReference) =>
        Pagamentos[paymentId] = new StatusPagamentoMp(paymentId, status, valor, externalReference);
}

public class NullAppLogger<T> : IAppLogger<T>
{
    public List<string> Avisos { get; } = new();
    public void LogInformation(string mensagem, params object[] args) { }
    public void LogWarning(string mensagem, params object[] args) => Avisos.Add(mensagem);
    public void LogError(Exception ex, string mensagem, params object[] args) { }
}
