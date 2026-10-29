using FluentAssertions;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Events;
using OficinaBilling.Domain.ValueObjects;
using Xunit;

namespace OficinaBilling.UnitTests.Entidades;

public class PagamentoTests
{
    private static Pagamento NovoPagamento() =>
        Pagamento.CriarCobranca(Guid.NewGuid(), Guid.NewGuid(), new Dinheiro(500m), "pref-1", "https://mp.test/pref-1");

    [Fact]
    public void Cobranca_nova_nasce_pendente()
    {
        var p = NovoPagamento();
        p.Status.Should().Be(StatusPagamento.Pendente);
        p.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Confirmar_muda_status_e_gera_um_evento()
    {
        var p = NovoPagamento();

        p.Confirmar("123").Should().BeTrue();

        p.Status.Should().Be(StatusPagamento.Confirmado);
        p.MercadoPagoId.Should().Be("123");
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PagamentoConfirmadoEvent>();
    }

    [Fact]
    public void Confirmar_duas_vezes_e_idempotente()
    {
        var p = NovoPagamento();
        p.Confirmar("123");

        p.Confirmar("123").Should().BeFalse();

        p.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Recusar_duas_vezes_e_idempotente()
    {
        var p = NovoPagamento();
        p.Recusar("1", "rejected").Should().BeTrue();

        p.Recusar("1", "rejected").Should().BeFalse();

        p.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Aprovacao_depois_de_recusa_confirma_porque_o_dinheiro_entrou()
    {
        // Checkout Pro permite nova tentativa (outro cartão) na mesma preferência.
        var p = NovoPagamento();
        p.Recusar("1", "rejected");

        p.Confirmar("2").Should().BeTrue();

        p.Status.Should().Be(StatusPagamento.Confirmado);
        p.MercadoPagoId.Should().Be("2");
        p.MotivoRecusa.Should().BeNull();
    }

    [Fact]
    public void Recusa_nao_desfaz_pagamento_ja_confirmado()
    {
        var p = NovoPagamento();
        p.Confirmar("1");

        p.Recusar("2", "rejected").Should().BeFalse();

        p.Status.Should().Be(StatusPagamento.Confirmado);
    }
}
