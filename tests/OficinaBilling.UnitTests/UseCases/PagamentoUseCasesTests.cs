using FluentAssertions;
using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Messages;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.Events;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.UseCases.Orcamento.AprovarOrcamento;
using OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;
using OficinaBilling.Application.UseCases.Pagamento.CancelarPorOs;
using OficinaBilling.Application.UseCases.Pagamento.ConsultarPagamento;
using OficinaBilling.Application.UseCases.Pagamento.CriarCobranca;
using OficinaBilling.Application.UseCases.Pagamento.ProcessarWebhookMercadoPago;
using OficinaBilling.Domain.Enums;
using OficinaBilling.UnitTests.Support;

namespace OficinaBilling.UnitTests.UseCases;

public class PagamentoUseCasesTests
{
    private readonly InMemoryOrcamentoRepository _orcamentos = new();
    private readonly InMemoryPagamentoRepository _pagamentos = new();
    private readonly FakeOutbox _outbox = new();
    private readonly FakeCadastros _cadastros = new();
    private readonly FakeMercadoPago _mp = new();
    private readonly CriarCobrancaUseCase _cobrar;
    private readonly ProcessarWebhookMercadoPagoUseCase _webhook;
    private readonly CancelarPorOsUseCase _cancelar;

    public PagamentoUseCasesTests()
    {
        _cobrar = new(_orcamentos, _pagamentos, _mp, new NullLogger<CriarCobrancaUseCase>());
        _webhook = new(_pagamentos, _mp, _outbox, new NullLogger<ProcessarWebhookMercadoPagoUseCase>());
        _cancelar = new(_orcamentos, _pagamentos, _mp, new NullLogger<CancelarPorOsUseCase>());
    }

    /// <summary>Cria orçamento de R$ 150 (1 peça de 50 + 1 serviço de 100) para uma nova OS.</summary>
    private async Task<(Guid Os, Guid OrcamentoId)> OrcamentoAsync(bool aprovado)
    {
        var os = Guid.NewGuid();
        var gerar = new GerarOrcamentoUseCase(_orcamentos, _cadastros, _outbox, new NullLogger<GerarOrcamentoUseCase>());
        var r = await gerar.ExecutarAsync(new GerarOrcamentoRequest(os,
            new() { new(_cadastros.AddPeca("Filtro", 50m), 1) }, new() { new(_cadastros.AddServico("Troca", 100m), 1) }));
        if (aprovado)
            await new AprovarOrcamentoUseCase(_orcamentos, _outbox).ExecutarAsync(r.Valor!.Id);
        return (os, r.Valor!.Id);
    }

    private async Task<(Guid Os, Guid PagamentoId)> CobrancaAsync()
    {
        var (os, _) = await OrcamentoAsync(aprovado: true);
        var r = await _cobrar.ExecutarAsync(new ExecucaoFinalizadaMessage(os));
        return (os, r.Valor!.Id);
    }

    private void MpPaga(string mpId, string status, Guid pagamentoId, decimal valor = 150m) =>
        _mp.Pagamentos[mpId] = new PagamentoMercadoPago(mpId, status, pagamentoId.ToString(), valor);

    private IEnumerable<string> Topicos => _outbox.Eventos.Select(EventCatalog.TopicoDe);

    // ---------- CriarCobranca ----------

    [Fact]
    public async Task Cobranca_cria_pagamento_pendente_com_link()
    {
        var (os, _) = await OrcamentoAsync(aprovado: true);

        var r = await _cobrar.ExecutarAsync(new ExecucaoFinalizadaMessage(os));

        r.Sucesso.Should().BeTrue();
        r.Valor!.Status.Should().Be("Pendente");
        r.Valor.Valor.Should().Be(150m);
        r.Valor.LinkPagamento.Should().StartWith("https://");
    }

    [Fact]
    public async Task Cobranca_e_idempotente()
    {
        var (os, _) = await OrcamentoAsync(aprovado: true);

        var a = await _cobrar.ExecutarAsync(new ExecucaoFinalizadaMessage(os));
        var b = await _cobrar.ExecutarAsync(new ExecucaoFinalizadaMessage(os));

        b.Valor!.Id.Should().Be(a.Valor!.Id);
        _mp.PreferenciasCriadas.Should().Be(1);
    }

    [Fact]
    public async Task Cobranca_exige_orcamento_aprovado()
    {
        var (os, _) = await OrcamentoAsync(aprovado: false);

        var r = await _cobrar.ExecutarAsync(new ExecucaoFinalizadaMessage(os));

        r.Tipo.Should().Be(TipoErro.Conflito);
        _mp.PreferenciasCriadas.Should().Be(0);
    }

    [Fact]
    public async Task Cobranca_sem_orcamento_e_nao_encontrado() =>
        (await _cobrar.ExecutarAsync(new ExecucaoFinalizadaMessage(Guid.NewGuid()))).Tipo.Should().Be(TipoErro.NaoEncontrado);

    [Fact]
    public async Task Cobranca_com_os_vazia_e_invalida() =>
        (await _cobrar.ExecutarAsync(new ExecucaoFinalizadaMessage(Guid.Empty))).Tipo.Should().Be(TipoErro.Invalido);

    [Fact]
    public async Task Consultar_pagamento_devolve_o_mais_recente()
    {
        var (os, id) = await CobrancaAsync();
        var consultar = new ConsultarPagamentoUseCase(_pagamentos);

        (await consultar.ExecutarAsync(os)).Valor!.Id.Should().Be(id);
        (await consultar.ExecutarAsync(Guid.NewGuid())).Tipo.Should().Be(TipoErro.NaoEncontrado);
    }

    // ---------- Webhook ----------

    [Fact]
    public async Task Webhook_aprovado_confirma_e_publica_uma_vez()
    {
        var (_, pagId) = await CobrancaAsync();
        MpPaga("999", "approved", pagId);

        (await _webhook.ExecutarAsync("999")).Sucesso.Should().BeTrue();
        await _webhook.ExecutarAsync("999"); // reentrega do MP

        Topicos.Count(t => t == "pagamento.confirmado").Should().Be(1);
        var p = await _pagamentos.ObterPorIdAsync(pagId);
        p!.Status.Should().Be(StatusPagamento.Confirmado);
        p.MercadoPagoPaymentId.Should().Be("999");
    }

    [Fact]
    public async Task Webhook_rejeitado_publica_pagamento_recusado()
    {
        var (_, pagId) = await CobrancaAsync();
        MpPaga("1", "rejected", pagId);

        await _webhook.ExecutarAsync("1");

        Topicos.Should().Contain("pagamento.recusado");
        (await _pagamentos.ObterPorIdAsync(pagId))!.Status.Should().Be(StatusPagamento.Recusado);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("in_process")]
    [InlineData("authorized")]
    public async Task Webhook_com_status_intermediario_nao_publica(string status)
    {
        var (_, pagId) = await CobrancaAsync();
        MpPaga("2", status, pagId);

        (await _webhook.ExecutarAsync("2")).Sucesso.Should().BeTrue();

        Topicos.Should().NotContain(t => t.StartsWith("pagamento."));
        (await _pagamentos.ObterPorIdAsync(pagId))!.Status.Should().Be(StatusPagamento.Pendente);
    }

    [Fact]
    public async Task Webhook_com_valor_divergente_nao_confirma()
    {
        var (_, pagId) = await CobrancaAsync();
        MpPaga("3", "approved", pagId, valor: 1m);

        var r = await _webhook.ExecutarAsync("3");

        r.Tipo.Should().Be(TipoErro.Conflito);
        (await _pagamentos.ObterPorIdAsync(pagId))!.Status.Should().Be(StatusPagamento.Pendente);
    }

    [Fact]
    public async Task Webhook_para_pagamento_inexistente_no_mp() =>
        (await _webhook.ExecutarAsync("nao-existe")).Tipo.Should().Be(TipoErro.NaoEncontrado);

    [Fact]
    public async Task Webhook_para_external_reference_desconhecida()
    {
        MpPaga("4", "approved", Guid.NewGuid());

        (await _webhook.ExecutarAsync("4")).Tipo.Should().Be(TipoErro.NaoEncontrado);
    }

    [Fact]
    public async Task Webhook_sem_id_e_invalido() =>
        (await _webhook.ExecutarAsync(" ")).Tipo.Should().Be(TipoErro.Invalido);

    [Fact]
    public async Task Webhook_encontra_pagamento_pelo_id_do_mp_quando_external_reference_nao_e_guid()
    {
        var (_, pagId) = await CobrancaAsync();
        (await _pagamentos.ObterPorIdAsync(pagId))!.RegistrarPagamentoMercadoPago("77");
        _mp.Pagamentos["77"] = new PagamentoMercadoPago("77", "approved", "texto-livre", 150m);

        (await _webhook.ExecutarAsync("77")).Sucesso.Should().BeTrue();

        (await _pagamentos.ObterPorIdAsync(pagId))!.Status.Should().Be(StatusPagamento.Confirmado);
    }

    [Fact]
    public async Task Webhook_estorno_no_mp_marca_estornado()
    {
        var (_, pagId) = await CobrancaAsync();
        MpPaga("5", "approved", pagId);
        await _webhook.ExecutarAsync("5");
        MpPaga("5", "refunded", pagId);

        await _webhook.ExecutarAsync("5");

        (await _pagamentos.ObterPorIdAsync(pagId))!.Status.Should().Be(StatusPagamento.Estornado);
    }

    [Fact]
    public async Task Webhook_aprovado_depois_de_recusa_estorna_automaticamente()
    {
        var (_, pagId) = await CobrancaAsync();
        MpPaga("6", "rejected", pagId);
        await _webhook.ExecutarAsync("6");
        MpPaga("7", "approved", pagId);

        var r = await _webhook.ExecutarAsync("7");

        r.Sucesso.Should().BeTrue();
        _mp.Estornos.Should().Contain("7");
        Topicos.Count(t => t == "pagamento.confirmado").Should().Be(0);
    }

    [Fact]
    public async Task Webhook_aprovado_apos_cancelamento_propaga_falha_do_estorno_para_retry()
    {
        var (os, pagId) = await CobrancaAsync();
        await _cancelar.ExecutarAsync(new OsCanceladaMessage(os));
        MpPaga("8", "approved", pagId);
        _mp.FalharAoEstornar = true;

        await FluentActions.Awaiting(() => _webhook.ExecutarAsync("8"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------- Compensação (os.cancelada) ----------

    [Fact]
    public async Task Cancelamento_cancela_orcamento_e_cobranca_pendente()
    {
        var (os, pagId) = await CobrancaAsync();

        (await _cancelar.ExecutarAsync(new OsCanceladaMessage(os))).Sucesso.Should().BeTrue();

        (await _orcamentos.ObterPorOrdemServicoIdAsync(os))!.Status.Should().Be(StatusOrcamento.Cancelado);
        (await _pagamentos.ObterPorIdAsync(pagId))!.Status.Should().Be(StatusPagamento.Cancelado);
        _mp.Estornos.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelamento_estorna_pagamento_confirmado()
    {
        var (os, pagId) = await CobrancaAsync();
        MpPaga("10", "approved", pagId);
        await _webhook.ExecutarAsync("10");

        await _cancelar.ExecutarAsync(new OsCanceladaMessage(os));

        _mp.Estornos.Should().Equal("10");
        (await _pagamentos.ObterPorIdAsync(pagId))!.Status.Should().Be(StatusPagamento.Estornado);
    }

    [Fact]
    public async Task Cancelamento_cancela_orcamento_sem_pagamento()
    {
        var (os, orcId) = await OrcamentoAsync(aprovado: false);

        await _cancelar.ExecutarAsync(new OsCanceladaMessage(os));

        (await _orcamentos.ObterPorIdAsync(orcId))!.Status.Should().Be(StatusOrcamento.Cancelado);
    }

    [Fact]
    public async Task Cancelamento_sem_nada_a_compensar_e_ok() =>
        (await _cancelar.ExecutarAsync(new OsCanceladaMessage(Guid.NewGuid()))).Sucesso.Should().BeTrue();

    [Fact]
    public async Task Cancelamento_e_idempotente()
    {
        var (os, _) = await CobrancaAsync();

        await _cancelar.ExecutarAsync(new OsCanceladaMessage(os));
        (await _cancelar.ExecutarAsync(new OsCanceladaMessage(os))).Sucesso.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelamento_com_os_vazia_e_invalido() =>
        (await _cancelar.ExecutarAsync(new OsCanceladaMessage(Guid.Empty))).Tipo.Should().Be(TipoErro.Invalido);

    [Fact]
    public async Task Cancelamento_de_pagamento_confirmado_sem_id_do_mp_exige_estorno_manual()
    {
        var (os, pagId) = await CobrancaAsync();
        (await _pagamentos.ObterPorIdAsync(pagId))!.Confirmar(); // confirmado sem passar pelo webhook

        var r = await _cancelar.ExecutarAsync(new OsCanceladaMessage(os));

        r.Tipo.Should().Be(TipoErro.Conflito);
    }

    [Fact]
    public async Task Cancelamento_propaga_falha_do_estorno_para_a_mensagem_ser_reprocessada()
    {
        var (os, pagId) = await CobrancaAsync();
        MpPaga("11", "approved", pagId);
        await _webhook.ExecutarAsync("11");
        _mp.FalharAoEstornar = true;

        await FluentActions.Awaiting(() => _cancelar.ExecutarAsync(new OsCanceladaMessage(os)))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
