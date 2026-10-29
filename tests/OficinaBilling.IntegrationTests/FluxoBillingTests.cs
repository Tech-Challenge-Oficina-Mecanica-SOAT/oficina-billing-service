using System.Net;
using FluentAssertions;
using OficinaBilling.IntegrationTests.Support;

namespace OficinaBilling.IntegrationTests;

/// <summary>API + PostgreSQL reais. Cada teste usa uma OS nova (o banco é compartilhado).</summary>
public class FluxoBillingTests : IAsyncLifetime
{
    private BillingDriver _d = null!;

    public async Task InitializeAsync() => _d = new BillingDriver(await TestHost.ObterAsync());
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> OsComOrcamentoAsync(int qtd = 2, decimal peca = 50m, decimal servico = 100m)
    {
        var os = Guid.NewGuid();
        (await _d.DiagnosticoConcluidoAsync(os, qtd, peca, servico)).Sucesso.Should().BeTrue();
        return os;
    }

    private async Task<(Guid Os, Guid PagamentoId)> OsComCobrancaAsync()
    {
        var os = await OsComOrcamentoAsync(); // 2 x 50 + 100 = 200
        var orcamento = await _d.ObterOrcamentoAsync(os);
        (await _d.AprovarAsync(orcamento.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _d.ExecucaoFinalizadaAsync(os)).Sucesso.Should().BeTrue();
        return (os, (await _d.ObterPagamentoAsync(os)).Id);
    }

    [Fact]
    public async Task Fluxo_completo_da_OS_ate_o_pagamento_confirmado()
    {
        var os = await OsComOrcamentoAsync();

        var orcamento = await _d.ObterOrcamentoAsync(os);
        orcamento.ValorTotal.Should().Be(200m);
        orcamento.Status.Should().Be("Gerado");
        orcamento.Itens.Should().HaveCount(2);

        (await _d.AprovarAsync(orcamento.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        await _d.ExecucaoFinalizadaAsync(os);

        var pagamento = await _d.ObterPagamentoAsync(os);
        pagamento.Status.Should().Be("Pendente");
        pagamento.Valor.Should().Be(200m);
        pagamento.LinkPagamento.Should().NotBeNullOrEmpty();

        (await _d.WebhookAsync($"fake-approved-{pagamento.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _d.ObterPagamentoAsync(os)).Status.Should().Be("Confirmado");

        await _d.PublicarOutboxAsync();
        _d.Eventos.TopicosDa(os).Should().Equal("orcamento.gerado", "orcamento.aprovado", "pagamento.confirmado");
    }

    [Fact]
    public async Task Mensagem_duplicada_nao_duplica_orcamento_nem_evento()
    {
        var os = Guid.NewGuid();
        await _d.DiagnosticoConcluidoAsync(os, 1, 10m, 20m);
        await _d.DiagnosticoConcluidoAsync(os, 1, 10m, 20m);
        await _d.PublicarOutboxAsync();

        _d.Eventos.TopicosDa(os).Should().Equal("orcamento.gerado");
    }

    [Fact]
    public async Task Webhook_duplicado_nao_republica_o_evento()
    {
        var (os, pagamentoId) = await OsComCobrancaAsync();

        await _d.WebhookAsync($"fake-approved-{pagamentoId}");
        await _d.WebhookAsync($"fake-approved-{pagamentoId}");
        await _d.PublicarOutboxAsync();

        _d.Eventos.TopicosDa(os).Count(t => t == "pagamento.confirmado").Should().Be(1);
    }

    [Fact]
    public async Task Mesma_idempotency_key_e_descartada_na_segunda_entrega()
    {
        var (_, pagamentoId) = await OsComCobrancaAsync();
        var chave = Guid.NewGuid().ToString();

        var primeira = await _d.WebhookAsync($"fake-approved-{pagamentoId}", chave);
        var segunda = await _d.WebhookAsync($"fake-approved-{pagamentoId}", chave);

        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        (await primeira.Content.ReadAsStringAsync()).Should().Contain("processado");
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("duplicado");
    }

    [Fact]
    public async Task Pagamento_recusado_publica_evento_de_compensacao()
    {
        var (os, pagamentoId) = await OsComCobrancaAsync();

        await _d.WebhookAsync($"fake-rejected-{pagamentoId}");
        await _d.PublicarOutboxAsync();

        (await _d.ObterPagamentoAsync(os)).Status.Should().Be("Recusado");
        _d.Eventos.TopicosDa(os).Should().Contain("pagamento.recusado");
    }

    [Fact]
    public async Task Orcamento_recusado_publica_evento_e_nao_gera_cobranca()
    {
        var os = await OsComOrcamentoAsync();
        var orcamento = await _d.ObterOrcamentoAsync(os);

        (await _d.RecusarAsync(orcamento.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _d.ExecucaoFinalizadaAsync(os)).Sucesso.Should().BeFalse();
        await _d.PublicarOutboxAsync();

        _d.Eventos.TopicosDa(os).Should().Equal("orcamento.gerado", "orcamento.recusado");
    }

    [Fact]
    public async Task Aprovar_duas_vezes_e_idempotente_e_aprovar_recusado_e_conflito()
    {
        var os = await OsComOrcamentoAsync();
        var id = (await _d.ObterOrcamentoAsync(os)).Id;

        (await _d.AprovarAsync(id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _d.AprovarAsync(id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _d.RecusarAsync(id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _d.PublicarOutboxAsync();

        _d.Eventos.TopicosDa(os).Count(t => t == "orcamento.aprovado").Should().Be(1);
    }

    [Fact]
    public async Task Saga_os_cancelada_cancela_orcamento_e_cobranca_pendente()
    {
        var (os, _) = await OsComCobrancaAsync();

        (await _d.OsCanceladaAsync(os)).Sucesso.Should().BeTrue();

        (await _d.ObterOrcamentoAsync(os)).Status.Should().Be("Cancelado");
        (await _d.ObterPagamentoAsync(os)).Status.Should().Be("Cancelado");
    }

    [Fact]
    public async Task Saga_os_cancelada_apos_pagamento_estorna()
    {
        var (os, pagamentoId) = await OsComCobrancaAsync();
        await _d.WebhookAsync($"fake-approved-{pagamentoId}");

        (await _d.OsCanceladaAsync(os)).Sucesso.Should().BeTrue();

        (await _d.ObterPagamentoAsync(os)).Status.Should().Be("Estornado");
    }

    [Fact]
    public async Task Consultas_de_recursos_inexistentes_retornam_404()
    {
        (await _d.ConsultarOrcamentoAsync(Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _d.AprovarAsync(Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _d.RecusarAsync(Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Webhook_de_outro_tipo_e_ignorado_e_sem_id_e_400()
    {
        (await _d.WebhookAsync("123", type: "merchant_order")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _d.WebhookSemIdAsync()).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_de_pagamento_desconhecido_responde_200_para_o_mp_nao_reenviar() =>
        (await _d.WebhookAsync("fake-approved-" + Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.OK);

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Health_checks_respondem_200(string caminho) =>
        (await _d.HealthAsync(caminho)).StatusCode.Should().Be(HttpStatusCode.OK);
}
