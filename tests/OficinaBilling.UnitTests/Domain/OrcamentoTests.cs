using FluentAssertions;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;
using Xunit;

namespace OficinaBilling.UnitTests.Entidades;

public class OrcamentoTests
{
    private static Orcamento NovoOrcamento(DateTime? agora = null) => Orcamento.Gerar(
        Guid.NewGuid(),
        new[]
        {
            OrcamentoItem.Criar(TipoItem.Peca, Guid.NewGuid(), "Pastilha", 2, 150m),
            OrcamentoItem.Criar(TipoItem.Servico, Guid.NewGuid(), "Troca de óleo", 1, 200m)
        },
        agora);

    [Fact]
    public void Gerar_soma_itens_e_nasce_Gerado()
    {
        var o = NovoOrcamento();
        o.ValorTotal.Valor.Should().Be(500m);
        o.Status.Should().Be(StatusOrcamento.Gerado);
        o.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Aprovar_orcamento_expirado_lanca_e_marca_Expirado()
    {
        var o = NovoOrcamento(agora: DateTime.UtcNow.AddDays(-30));

        var act = () => o.Aprovar();

        act.Should().Throw<InvalidOperationException>();
        o.Status.Should().Be(StatusOrcamento.Expirado);
    }

    [Fact]
    public void Aprovar_duas_vezes_falha()
    {
        var o = NovoOrcamento();
        o.Aprovar();

        var act = () => o.Aprovar();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Compensacao_so_cancela_orcamento_Gerado()
    {
        var gerado = NovoOrcamento();
        gerado.CancelarPorCompensacao().Should().BeTrue();
        gerado.Status.Should().Be(StatusOrcamento.Cancelado);

        var aprovado = NovoOrcamento();
        aprovado.Aprovar();
        aprovado.CancelarPorCompensacao().Should().BeFalse();
        aprovado.Status.Should().Be(StatusOrcamento.Aprovado);
    }
}
