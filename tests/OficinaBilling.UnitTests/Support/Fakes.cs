using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.UnitTests.Support;

public sealed class NullLogger<T> : IAppLogger<T>
{
    public void LogInformation(string mensagem, params object[] args) { }
    public void LogWarning(string mensagem, params object[] args) { }
    public void LogError(Exception ex, string mensagem, params object[] args) { }
}

public sealed class FakeOutbox : IOutbox
{
    public List<object> Eventos { get; } = new();
    public void Enfileirar(object evento) => Eventos.Add(evento);
}

public sealed class FakeCadastros : ICadastrosServiceClient
{
    public Dictionary<Guid, PrecoItem> Pecas { get; } = new();
    public Dictionary<Guid, PrecoItem> Servicos { get; } = new();

    public Guid AddPeca(string descricao, decimal preco)
    {
        var id = Guid.NewGuid();
        Pecas[id] = new PrecoItem(id, descricao, preco);
        return id;
    }

    public Guid AddServico(string descricao, decimal preco)
    {
        var id = Guid.NewGuid();
        Servicos[id] = new PrecoItem(id, descricao, preco);
        return id;
    }

    public Task<PrecoItem?> ObterPrecoPecaAsync(Guid pecaId, CancellationToken ct = default) =>
        Task.FromResult(Pecas.GetValueOrDefault(pecaId));

    public Task<PrecoItem?> ObterPrecoServicoAsync(Guid servicoId, CancellationToken ct = default) =>
        Task.FromResult(Servicos.GetValueOrDefault(servicoId));
}

public sealed class FakeMercadoPago : IMercadoPagoGateway
{
    public int PreferenciasCriadas { get; private set; }
    public Dictionary<string, PagamentoMercadoPago> Pagamentos { get; } = new();
    public List<string> Estornos { get; } = new();
    public bool FalharAoEstornar { get; set; }

    public Task<CobrancaCriada> CriarCobrancaAsync(Guid pagamentoId, decimal valor, string descricao, CancellationToken ct = default)
    {
        PreferenciasCriadas++;
        return Task.FromResult(new CobrancaCriada($"pref-{pagamentoId}", $"https://mp.test/checkout/{pagamentoId}"));
    }

    public Task<PagamentoMercadoPago?> ObterPagamentoAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(Pagamentos.GetValueOrDefault(id));

    public Task EstornarAsync(string id, string idempotencyKey, CancellationToken ct = default)
    {
        if (FalharAoEstornar) throw new InvalidOperationException("MP indisponível");
        Estornos.Add(id);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryOrcamentoRepository : IOrcamentoRepository
{
    public List<Orcamento> Itens { get; } = new();
    public int Salvamentos { get; private set; }

    public Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(o => o.Id == id));

    public Task<Orcamento?> ObterPorOrdemServicoIdAsync(Guid osId, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(o => o.OrdemServicoId == osId));

    public Task AdicionarAsync(Orcamento orcamento, CancellationToken ct = default)
    {
        Itens.Add(orcamento);
        return Task.CompletedTask;
    }

    public Task SalvarAlteracoesAsync(CancellationToken ct = default)
    {
        Salvamentos++;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPagamentoRepository : IPagamentoRepository
{
    public List<Pagamento> Itens { get; } = new();

    public Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(p => p.Id == id));

    public Task<Pagamento?> ObterPorMercadoPagoIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(p => p.MercadoPagoPaymentId == id));

    public Task<Pagamento?> ObterUltimoPorOrdemServicoIdAsync(Guid osId, CancellationToken ct = default) =>
        Task.FromResult(Itens.Where(p => p.OrdemServicoId == osId).OrderByDescending(p => p.CriadoEm).FirstOrDefault());

    public Task<Pagamento?> ObterAtivoPorOrdemServicoIdAsync(Guid osId, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(p => p.OrdemServicoId == osId
            && p.Status is StatusPagamento.Pendente or StatusPagamento.Confirmado));

    public Task<IReadOnlyList<Pagamento>> ListarPorOrdemServicoIdAsync(Guid osId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Pagamento>>(Itens.Where(p => p.OrdemServicoId == osId).ToList());

    public Task AdicionarAsync(Pagamento pagamento, CancellationToken ct = default)
    {
        Itens.Add(pagamento);
        return Task.CompletedTask;
    }

    public Task SalvarAlteracoesAsync(CancellationToken ct = default) => Task.CompletedTask;
}
