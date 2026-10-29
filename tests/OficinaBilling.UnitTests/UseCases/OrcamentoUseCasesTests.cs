using FluentAssertions;
using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.Events;
using OficinaBilling.Application.UseCases.Orcamento.AprovarOrcamento;
using OficinaBilling.Application.UseCases.Orcamento.ConsultarOrcamento;
using OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;
using OficinaBilling.Application.UseCases.Orcamento.RecusarOrcamento;
using OficinaBilling.UnitTests.Support;

namespace OficinaBilling.UnitTests.UseCases;

public class OrcamentoUseCasesTests
{
    private readonly InMemoryOrcamentoRepository _repo = new();
    private readonly FakeOutbox _outbox = new();
    private readonly FakeCadastros _cadastros = new();
    private readonly GerarOrcamentoUseCase _gerar;

    public OrcamentoUseCasesTests() =>
        _gerar = new GerarOrcamentoUseCase(_repo, _cadastros, _outbox, new NullLogger<GerarOrcamentoUseCase>());

    private async Task<Guid> OrcamentoGeradoAsync(Guid? os = null)
    {
        var peca = _cadastros.AddPeca("Filtro", 50m);
        var servico = _cadastros.AddServico("Troca", 100m);
        var r = await _gerar.ExecutarAsync(new GerarOrcamentoRequest(os ?? Guid.NewGuid(),
            new() { new(peca, 2) }, new() { new(servico, 1) }));
        return r.Valor!.Id;
    }

    [Fact]
    public async Task Gerar_soma_pecas_e_servicos_e_enfileira_evento()
    {
        var peca = _cadastros.AddPeca("Filtro", 50m);
        var servico = _cadastros.AddServico("Troca", 100m);

        var r = await _gerar.ExecutarAsync(new GerarOrcamentoRequest(Guid.NewGuid(), new() { new(peca, 2) }, new() { new(servico, 1) }));

        r.Sucesso.Should().BeTrue();
        r.Valor!.ValorTotal.Should().Be(200m);
        r.Valor.Itens.Should().HaveCount(2);
        _outbox.Eventos.Should().ContainSingle();
        EventCatalog.TopicoDe(_outbox.Eventos[0]).Should().Be("orcamento.gerado");
        _repo.Salvamentos.Should().Be(1);
    }

    [Fact]
    public async Task Gerar_e_idempotente_para_a_mesma_os()
    {
        var os = Guid.NewGuid();
        var peca = _cadastros.AddPeca("Filtro", 50m);
        var req = new GerarOrcamentoRequest(os, new() { new(peca, 1) }, new());

        var primeiro = await _gerar.ExecutarAsync(req);
        var segundo = await _gerar.ExecutarAsync(req);

        segundo.Valor!.Id.Should().Be(primeiro.Valor!.Id);
        _repo.Itens.Should().ContainSingle();
        _outbox.Eventos.Should().ContainSingle();
    }

    [Fact]
    public async Task Gerar_falha_quando_peca_nao_existe()
    {
        var r = await _gerar.ExecutarAsync(new GerarOrcamentoRequest(Guid.NewGuid(), new() { new(Guid.NewGuid(), 1) }, new()));

        r.Sucesso.Should().BeFalse();
        r.Tipo.Should().Be(TipoErro.NaoEncontrado);
        _outbox.Eventos.Should().BeEmpty();
        _repo.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task Gerar_falha_quando_servico_nao_existe()
    {
        var r = await _gerar.ExecutarAsync(new GerarOrcamentoRequest(Guid.NewGuid(), new(), new() { new(Guid.NewGuid(), 1) }));

        r.Tipo.Should().Be(TipoErro.NaoEncontrado);
    }

    [Theory]
    [InlineData(true, false)]   // OS vazia
    [InlineData(false, true)]   // sem itens
    public async Task Gerar_valida_entrada(bool osVazia, bool semItens)
    {
        var peca = _cadastros.AddPeca("Filtro", 50m);
        var req = new GerarOrcamentoRequest(osVazia ? Guid.Empty : Guid.NewGuid(),
            semItens ? new() : new() { new(peca, 1) }, null!);

        (await _gerar.ExecutarAsync(req)).Tipo.Should().Be(TipoErro.Invalido);
    }

    [Fact]
    public async Task Gerar_rejeita_quantidade_invalida()
    {
        var peca = _cadastros.AddPeca("Filtro", 50m);

        var r = await _gerar.ExecutarAsync(new GerarOrcamentoRequest(Guid.NewGuid(), new() { new(peca, 0) }, new()));

        r.Tipo.Should().Be(TipoErro.Invalido);
    }

    [Fact]
    public async Task Aprovar_enfileira_evento_uma_unica_vez()
    {
        var id = await OrcamentoGeradoAsync();
        var aprovar = new AprovarOrcamentoUseCase(_repo, _outbox);

        var r1 = await aprovar.ExecutarAsync(id);
        var r2 = await aprovar.ExecutarAsync(id);

        r1.Valor!.Status.Should().Be("Aprovado");
        r2.Sucesso.Should().BeTrue();
        _outbox.Eventos.Select(EventCatalog.TopicoDe).Should().Equal("orcamento.gerado", "orcamento.aprovado");
    }

    [Fact]
    public async Task Aprovar_inexistente_retorna_nao_encontrado() =>
        (await new AprovarOrcamentoUseCase(_repo, _outbox).ExecutarAsync(Guid.NewGuid())).Tipo.Should().Be(TipoErro.NaoEncontrado);

    [Fact]
    public async Task Recusar_enfileira_evento_e_e_idempotente()
    {
        var id = await OrcamentoGeradoAsync();
        var recusar = new RecusarOrcamentoUseCase(_repo, _outbox);

        (await recusar.ExecutarAsync(id)).Valor!.Status.Should().Be("Recusado");
        (await recusar.ExecutarAsync(id)).Sucesso.Should().BeTrue();

        _outbox.Eventos.Select(EventCatalog.TopicoDe).Should().Equal("orcamento.gerado", "orcamento.recusado");
    }

    [Fact]
    public async Task Recusar_orcamento_aprovado_e_conflito()
    {
        var id = await OrcamentoGeradoAsync();
        await new AprovarOrcamentoUseCase(_repo, _outbox).ExecutarAsync(id);

        (await new RecusarOrcamentoUseCase(_repo, _outbox).ExecutarAsync(id)).Tipo.Should().Be(TipoErro.Conflito);
    }

    [Fact]
    public async Task Aprovar_orcamento_recusado_e_conflito()
    {
        var id = await OrcamentoGeradoAsync();
        await new RecusarOrcamentoUseCase(_repo, _outbox).ExecutarAsync(id);

        (await new AprovarOrcamentoUseCase(_repo, _outbox).ExecutarAsync(id)).Tipo.Should().Be(TipoErro.Conflito);
    }

    [Fact]
    public async Task Recusar_inexistente_retorna_nao_encontrado() =>
        (await new RecusarOrcamentoUseCase(_repo, _outbox).ExecutarAsync(Guid.NewGuid())).Tipo.Should().Be(TipoErro.NaoEncontrado);

    [Fact]
    public async Task Consultar_por_os()
    {
        var os = Guid.NewGuid();
        await OrcamentoGeradoAsync(os);
        var consultar = new ConsultarOrcamentoUseCase(_repo);

        var ok = await consultar.ExecutarAsync(os);
        var nao = await consultar.ExecutarAsync(Guid.NewGuid());

        ok.Valor!.OrdemServicoId.Should().Be(os);
        ok.Valor.Itens.Should().HaveCount(2);
        nao.Tipo.Should().Be(TipoErro.NaoEncontrado);
    }
}
