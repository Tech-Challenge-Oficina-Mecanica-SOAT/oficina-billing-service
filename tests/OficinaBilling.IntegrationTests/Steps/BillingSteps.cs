using System.Net;
using FluentAssertions;
using OficinaBilling.IntegrationTests.Support;
using Reqnroll;

namespace OficinaBilling.IntegrationTests.Steps;

[Binding]
public class BillingSteps
{
    private BillingDriver _d = null!;
    private Guid _os;

    [BeforeScenario]
    public async Task Preparar()
    {
        _d = new BillingDriver(await TestHost.ObterAsync());
        _os = Guid.NewGuid();
    }

    [Given(@"uma ordem de serviço com diagnóstico de (\d+) peças de (\d+) reais e 1 serviço de (\d+) reais")]
    public async Task DadoDiagnostico(int quantidade, decimal precoPeca, decimal precoServico) =>
        (await _d.DiagnosticoConcluidoAsync(_os, quantidade, precoPeca, precoServico)).Sucesso.Should().BeTrue();

    [Given(@"o cliente aprovou o orçamento")]
    [When(@"o cliente aprova o orçamento")]
    public async Task QuandoAprova()
    {
        var orcamento = await _d.ObterOrcamentoAsync(_os);
        (await _d.AprovarAsync(orcamento.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [When(@"o cliente recusa o orçamento")]
    public async Task QuandoRecusa()
    {
        var orcamento = await _d.ObterOrcamentoAsync(_os);
        (await _d.RecusarAsync(orcamento.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Given(@"a execução da OS foi finalizada")]
    public async Task DadoExecucaoFinalizada() =>
        (await _d.ExecucaoFinalizadaAsync(_os)).Sucesso.Should().BeTrue();

    [When(@"o Mercado Pago notifica o pagamento como ""(.*)""")]
    public async Task QuandoWebhook(string status)
    {
        var pagamento = await _d.ObterPagamentoAsync(_os);
        (await _d.WebhookAsync($"fake-{status}-{pagamento.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [When(@"a OS é cancelada")]
    public async Task QuandoOsCancelada() =>
        (await _d.OsCanceladaAsync(_os)).Sucesso.Should().BeTrue();

    [Then(@"o orçamento da OS está ""(.*)"" com valor total de (\d+) reais")]
    public async Task EntaoOrcamento(string status, decimal total)
    {
        var orcamento = await _d.ObterOrcamentoAsync(_os);
        orcamento.Status.Should().Be(status);
        orcamento.ValorTotal.Should().Be(total);
    }

    [Then(@"o pagamento da OS está ""(.*)""")]
    public async Task EntaoPagamento(string status) =>
        (await _d.ObterPagamentoAsync(_os)).Status.Should().Be(status);

    [Then(@"o evento ""(.*)"" foi publicado")]
    public async Task EntaoEvento(string topico)
    {
        await _d.PublicarOutboxAsync();
        _d.Eventos.TopicosDa(_os).Should().Contain(topico);
    }

    [Then(@"o evento ""(.*)"" foi publicado (\d+) vez")]
    public async Task EntaoEventoNVezes(string topico, int vezes)
    {
        await _d.PublicarOutboxAsync();
        _d.Eventos.TopicosDa(_os).Count(t => t == topico).Should().Be(vezes);
    }
}
