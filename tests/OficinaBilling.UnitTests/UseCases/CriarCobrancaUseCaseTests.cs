using FluentAssertions;
using OficinaBilling.Application.UseCases.Pagamento;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.ValueObjects;
using OficinaBilling.UnitTests.Fakes;
using Xunit;

namespace OficinaBilling.UnitTests.UseCases;

public class CriarCobrancaUseCaseTests
{
    private readonly InMemoryOrcamentoRepository _orcamentos = new();
    private readonly InMemoryPagamentoRepository _pagamentos = new();
    private readonly FakeMercadoPagoGateway _gateway = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly CriarCobrancaUseCase _useCase;

    public CriarCobrancaUseCaseTests() =>
        _useCase = new CriarCobrancaUseCase(_orcamentos, _pagamentos, _gateway, _uow, new NullAppLogger<CriarCobrancaUseCase>());

    private Orcamento NovoOrcamento(bool aprovar)
    {
        var o = Orcamento.Gerar(Guid.NewGuid(), new[] { OrcamentoItem.Criar(TipoItem.Servico, Guid.NewGuid(), "Revisão", 1, 300m) });
        if (aprovar) o.Aprovar();
        _orcamentos.Itens.Add(o);
        return o;
    }

    [Fact]
    public async Task Orcamento_aprovado_gera_cobranca_pendente_com_external_reference_do_orcamento()
    {
        var o = NovoOrcamento(aprovar: true);

        var r = await _useCase.ExecutarAsync(o.OsId);

        r.Sucesso.Should().BeTrue();
        _pagamentos.Itens.Should().ContainSingle();
        _pagamentos.Itens[0].Status.Should().Be(StatusPagamento.Pendente);
        _pagamentos.Itens[0].Valor.Valor.Should().Be(300m);
        _gateway.PreferenciasCriadas.Should().ContainSingle().Which.ReferenciaExterna.Should().Be(o.Id);
    }

    [Fact]
    public async Task Orcamento_nao_aprovado_nao_gera_cobranca()
    {
        // Regressão: antes só se checava se o orçamento existia.
        var o = NovoOrcamento(aprovar: false);

        var r = await _useCase.ExecutarAsync(o.OsId);

        r.Sucesso.Should().BeFalse();
        r.Erro.Should().Contain("Aprovado");
        _pagamentos.Itens.Should().BeEmpty();
        _gateway.PreferenciasCriadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sem_orcamento_falha()
    {
        var r = await _useCase.ExecutarAsync(Guid.NewGuid());
        r.Sucesso.Should().BeFalse();
    }

    [Fact]
    public async Task Evento_duplicado_nao_cria_segunda_preferencia()
    {
        var o = NovoOrcamento(aprovar: true);

        await _useCase.ExecutarAsync(o.OsId);
        var segunda = await _useCase.ExecutarAsync(o.OsId);

        segunda.Sucesso.Should().BeTrue();
        _pagamentos.Itens.Should().HaveCount(1);
        _gateway.PreferenciasCriadas.Should().HaveCount(1);
    }
}
