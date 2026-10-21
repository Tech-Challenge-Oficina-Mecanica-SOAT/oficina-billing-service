using FluentAssertions;
using OficinaBilling.Application.UseCases.Orcamento;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Events;
using OficinaBilling.UnitTests.Fakes;
using Xunit;

namespace OficinaBilling.UnitTests.UseCases;

public class AprovarOrcamentoUseCaseTests
{
    private readonly InMemoryOrcamentoRepository _orcamentos = new();
    private readonly FakeEventPublisher _eventos = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly AprovarOrcamentoUseCase _useCase;

    public AprovarOrcamentoUseCaseTests() => _useCase = new AprovarOrcamentoUseCase(_orcamentos, _eventos, _uow);

    private Orcamento Novo(DateTime? agora = null)
    {
        var o = Orcamento.Gerar(Guid.NewGuid(), new[] { OrcamentoItem.Criar(TipoItem.Servico, Guid.NewGuid(), "Revisão", 1, 300m) }, agora);
        _orcamentos.Itens.Add(o);
        return o;
    }

    [Fact]
    public async Task Aprovar_orcamento_valido_salva_e_publica()
    {
        var o = Novo();

        var r = await _useCase.ExecutarAsync(o.Id);

        r.Sucesso.Should().BeTrue();
        o.Status.Should().Be(StatusOrcamento.Aprovado);
        _eventos.Eventos.OfType<OrcamentoAprovadoEvent>().Should().HaveCount(1);
        _uow.Salvamentos.Should().Be(1);
    }

    [Fact]
    public async Task Aprovar_orcamento_vencido_persiste_o_status_Expirado()
    {
        // Regressão: antes o use case retornava Falha sem salvar, e o "Expirado" se perdia.
        var o = Novo(agora: DateTime.UtcNow.AddDays(-30));

        var r = await _useCase.ExecutarAsync(o.Id);

        r.Sucesso.Should().BeFalse();
        o.Status.Should().Be(StatusOrcamento.Expirado);
        _uow.Salvamentos.Should().Be(1);
        _eventos.Eventos.Should().BeEmpty();
    }
}
