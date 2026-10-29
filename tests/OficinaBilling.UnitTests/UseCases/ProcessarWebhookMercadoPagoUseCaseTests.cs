using FluentAssertions;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.UseCases.Pagamento;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Events;
using OficinaBilling.Domain.ValueObjects;
using OficinaBilling.UnitTests.Fakes;
using Xunit;

namespace OficinaBilling.UnitTests.UseCases;

public class ProcessarWebhookMercadoPagoUseCaseTests
{
    private readonly InMemoryPagamentoRepository _pagamentos = new();
    private readonly InMemoryIdempotenciaRepository _idempotencia = new();
    private readonly FakeMercadoPagoGateway _gateway = new();
    private readonly FakeEventPublisher _eventos = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly ProcessarWebhookMercadoPagoUseCase _useCase;

    private readonly Pagamento _pagamento;

    public ProcessarWebhookMercadoPagoUseCaseTests()
    {
        _useCase = new ProcessarWebhookMercadoPagoUseCase(
            _pagamentos, _gateway, _eventos, _idempotencia, _uow, new NullAppLogger<ProcessarWebhookMercadoPagoUseCase>());

        _pagamento = Pagamento.CriarCobranca(Guid.NewGuid(), Guid.NewGuid(), new Dinheiro(500m), "pref-1", "https://mp.test/pref-1");
        _pagamentos.Itens.Add(_pagamento);
    }

    private string ExternalRef => _pagamento.OrcamentoId.ToString();

    [Fact]
    public async Task Aprovado_encontra_o_pagamento_pelo_external_reference_e_confirma()
    {
        // Regressão do bug principal: antes, o payment id era comparado com o PreferenceId e NUNCA casava.
        _gateway.Define("111", "approved", 500m, ExternalRef);

        var r = await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));

        r.Sucesso.Should().BeTrue();
        _pagamento.Status.Should().Be(StatusPagamento.Confirmado);
        _pagamento.MercadoPagoId.Should().Be("111");
        _eventos.Eventos.Should().ContainSingle().Which.Should().BeOfType<PagamentoConfirmadoEvent>();
    }

    [Fact]
    public async Task Mesma_notificacao_duas_vezes_gera_um_unico_evento()
    {
        _gateway.Define("111", "approved", 500m, ExternalRef);

        await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));
        var segunda = await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));

        segunda.Sucesso.Should().BeTrue();
        _eventos.Eventos.Should().HaveCount(1);
        _idempotencia.Chaves.Should().Contain("mp:111:approved");
    }

    [Fact]
    public async Task Pending_nao_altera_nada_e_aprovacao_posterior_ainda_e_processada()
    {
        _gateway.Define("111", "pending", 500m, ExternalRef);
        await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));
        _pagamento.Status.Should().Be(StatusPagamento.Pendente);
        _eventos.Eventos.Should().BeEmpty();

        _gateway.Define("111", "approved", 500m, ExternalRef); // mesmo payment, novo status
        await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));

        _pagamento.Status.Should().Be(StatusPagamento.Confirmado);
        _eventos.Eventos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Rejeitado_recusa_e_publica_evento()
    {
        _gateway.Define("111", "rejected", 500m, ExternalRef);

        await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));

        _pagamento.Status.Should().Be(StatusPagamento.Recusado);
        _eventos.Eventos.Should().ContainSingle().Which.Should().BeOfType<PagamentoRecusadoEvent>();
    }

    [Fact]
    public async Task Recusa_seguida_de_nova_tentativa_aprovada_confirma()
    {
        _gateway.Define("111", "rejected", 500m, ExternalRef);
        await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));

        _gateway.Define("222", "approved", 500m, ExternalRef); // novo payment, mesma preferência
        await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("222"));

        _pagamento.Status.Should().Be(StatusPagamento.Confirmado);
        _pagamento.MercadoPagoId.Should().Be("222");
    }

    [Fact]
    public async Task Valor_diferente_do_cobrado_nao_confirma()
    {
        _gateway.Define("111", "approved", 1m, ExternalRef);

        var r = await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));

        r.Sucesso.Should().BeFalse();
        r.Erro.Should().Contain("Valor");
        _pagamento.Status.Should().Be(StatusPagamento.Pendente);
        _eventos.Eventos.Should().BeEmpty();
        _idempotencia.Chaves.Should().BeEmpty(); // não marca: precisa poder ser reavaliado
    }

    [Fact]
    public async Task Payment_inexistente_no_mercado_pago_falha()
    {
        var r = await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("999"));

        r.Sucesso.Should().BeFalse();
        _eventos.Eventos.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nao-e-guid")]
    public async Task Payment_sem_external_reference_valido_falha(string? externalReference)
    {
        _gateway.Define("111", "approved", 500m, externalReference);

        var r = await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));

        r.Sucesso.Should().BeFalse();
        _pagamento.Status.Should().Be(StatusPagamento.Pendente);
    }

    [Fact]
    public async Task External_reference_de_orcamento_sem_pagamento_falha()
    {
        _gateway.Define("111", "approved", 500m, Guid.NewGuid().ToString());

        var r = await _useCase.ExecutarAsync(new WebhookMercadoPagoRequest("111"));

        r.Sucesso.Should().BeFalse();
        _pagamento.Status.Should().Be(StatusPagamento.Pendente);
    }
}
