using FluentAssertions;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Events;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.UnitTests.Domain;

public class DinheiroTests
{
    [Fact]
    public void Arredonda_para_duas_casas() => new Dinheiro(10.005m).Valor.Should().Be(10.01m);

    [Fact]
    public void Nao_aceita_negativo() =>
        FluentActions.Invoking(() => new Dinheiro(-1)).Should().Throw<ArgumentException>();

    [Fact]
    public void Soma_valores() => (new Dinheiro(1.10m) + new Dinheiro(2.20m)).Valor.Should().Be(3.30m);
}

public class OrcamentoTests
{
    private static Orcamento Novo() => Orcamento.Gerar(Guid.NewGuid(), new[]
    {
        new ItemOrcamento("Filtro", 2, new Dinheiro(50)),
        new ItemOrcamento("Mão de obra", 1, new Dinheiro(100))
    });

    [Fact]
    public void Gerar_soma_itens_e_emite_evento()
    {
        var o = Novo();

        o.ValorTotal.Valor.Should().Be(200m);
        o.Status.Should().Be(StatusOrcamento.Gerado);
        o.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<OrcamentoGeradoEvent>();
    }

    [Fact]
    public void Gerar_sem_itens_falha() =>
        FluentActions.Invoking(() => Orcamento.Gerar(Guid.NewGuid(), Array.Empty<ItemOrcamento>()))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Aprovar_muda_status_e_emite_evento()
    {
        var o = Novo();
        o.LimparEventos();

        o.Aprovar();

        o.Status.Should().Be(StatusOrcamento.Aprovado);
        o.AprovadoEm.Should().NotBeNull();
        o.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<OrcamentoAprovadoEvent>();
    }

    [Fact]
    public void Recusar_emite_evento()
    {
        var o = Novo();
        o.LimparEventos();

        o.Recusar();

        o.Status.Should().Be(StatusOrcamento.Recusado);
        o.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<OrcamentoRecusadoEvent>();
    }

    [Fact]
    public void Nao_aprova_nem_recusa_duas_vezes()
    {
        var o = Novo();
        o.Aprovar();

        FluentActions.Invoking(o.Aprovar).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(o.Recusar).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancelar_e_idempotente(bool aprovar)
    {
        var o = Novo();
        if (aprovar) o.Aprovar();

        o.Cancelar().Should().BeTrue();
        o.Status.Should().Be(StatusOrcamento.Cancelado);
        o.CanceladoEm.Should().NotBeNull();
        o.Cancelar().Should().BeFalse();
    }

    [Fact]
    public void Nao_cancela_orcamento_recusado()
    {
        var o = Novo();
        o.Recusar();

        o.Cancelar().Should().BeFalse();
        o.Status.Should().Be(StatusOrcamento.Recusado);
    }
}

public class PagamentoTests
{
    private static Pagamento Novo() => Pagamento.Criar(Guid.NewGuid(), Guid.NewGuid(), new Dinheiro(200));

    [Fact]
    public void Criar_gera_pagamento_pendente()
    {
        var p = Novo();

        p.Status.Should().Be(StatusPagamento.Pendente);
        p.Id.Should().NotBeEmpty();
        p.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Criar_com_valor_zero_falha() =>
        FluentActions.Invoking(() => Pagamento.Criar(Guid.NewGuid(), Guid.NewGuid(), Dinheiro.Zero))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Registra_cobranca()
    {
        var p = Novo();
        p.RegistrarCobranca("pref-1", "https://mp/checkout");

        p.MercadoPagoPreferenceId.Should().Be("pref-1");
        p.LinkPagamento.Should().Be("https://mp/checkout");
    }

    [Fact]
    public void Confirmar_emite_evento_uma_unica_vez()
    {
        var p = Novo();

        p.Confirmar().Should().BeTrue();
        p.Confirmar().Should().BeFalse();

        p.Status.Should().Be(StatusPagamento.Confirmado);
        p.ConfirmadoEm.Should().NotBeNull();
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PagamentoConfirmadoEvent>();
    }

    [Fact]
    public void Recusar_emite_evento_e_nao_confirma_depois()
    {
        var p = Novo();

        p.Recusar().Should().BeTrue();
        p.Confirmar().Should().BeFalse();

        p.Status.Should().Be(StatusPagamento.Recusado);
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PagamentoRecusadoEvent>();
    }

    [Fact]
    public void Cancelar_so_vale_para_pendente()
    {
        var pendente = Novo();
        pendente.Cancelar().Should().BeTrue();
        pendente.Status.Should().Be(StatusPagamento.Cancelado);

        var confirmado = Novo();
        confirmado.Confirmar();
        confirmado.Cancelar().Should().BeFalse();
    }

    [Fact]
    public void Estornar_so_vale_para_confirmado()
    {
        var p = Novo();
        p.Estornar().Should().BeFalse();

        p.Confirmar();
        p.Estornar().Should().BeTrue();
        p.Status.Should().Be(StatusPagamento.Estornado);
    }
}
