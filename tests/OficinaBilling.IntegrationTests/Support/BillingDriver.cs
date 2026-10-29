using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Messaging;
using OficinaBilling.Infrastructure.Messaging;

namespace OficinaBilling.IntegrationTests.Support;

/// <summary>
/// Faz o papel dos outros serviços e do Mercado Pago: entrega mensagens "como o SQS entregaria"
/// (envelope SNS → parser → handler, o mesmo caminho do consumidor real) e dispara o webhook via HTTP.
/// </summary>
public sealed class BillingDriver
{
    private readonly TestHost _host;
    private readonly HttpClient _http;

    public BillingDriver(TestHost host)
    {
        _host = host;
        _http = host.CreateClient();
    }

    public RecordingEventPublisher Eventos => _host.Eventos;

    // ---- mensagens que chegariam do SQS ----

    public Task<Result<bool>> DiagnosticoConcluidoAsync(Guid os, int quantidadePecas, decimal precoPeca, decimal precoServico)
    {
        var peca = _host.Cadastros.Registrar("Pastilha de freio", precoPeca);
        var servico = _host.Cadastros.Registrar("Troca de pastilhas", precoServico);
        return EntregarAsync("diagnostico.concluido", new
        {
            osId = os,
            pecas = new[] { new { pecaId = peca, quantidade = quantidadePecas } },
            servicos = new[] { new { servicoId = servico, quantidade = 1 } }
        });
    }

    public Task<Result<bool>> ExecucaoFinalizadaAsync(Guid os) => EntregarAsync("execucao.finalizada", new { osId = os });

    public Task<Result<bool>> OsCanceladaAsync(Guid os) => EntregarAsync("os.cancelada", new { osId = os });

    private async Task<Result<bool>> EntregarAsync(string topico, object payload)
    {
        var corpo = JsonSerializer.Serialize(new
        {
            Type = "Notification",
            TopicArn = $"arn:aws:sns:us-east-1:000000000000:oficina-test-{topico.Replace('.', '-')}",
            Message = JsonSerializer.Serialize(new { eventType = topico, eventVersion = "1.0", producer = "test", data = payload })
        });

        var mensagem = MensagemEntrante.Parse(corpo);
        using var scope = _host.Services.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IMensagemHandler>().Single(h => h.Topico == mensagem.Topico);
        return await handler.ProcessarAsync(mensagem.PayloadJson);
    }

    // ---- outbox ----

    public async Task PublicarOutboxAsync()
    {
        var processor = _host.Services.GetRequiredService<OutboxProcessor>();
        for (var i = 0; i < 20 && await processor.ProcessarLoteAsync(CancellationToken.None) > 0; i++) { }
    }

    // ---- REST ----

    public async Task<OrcamentoResponse> ObterOrcamentoAsync(Guid os) =>
        (await _http.GetFromJsonAsync<OrcamentoResponse>($"/orcamentos/{os}", JsonDefaults.Options))!;

    public async Task<PagamentoResponse> ObterPagamentoAsync(Guid os) =>
        (await _http.GetFromJsonAsync<PagamentoResponse>($"/pagamentos/{os}", JsonDefaults.Options))!;

    public Task<HttpResponseMessage> ConsultarOrcamentoAsync(Guid os) => _http.GetAsync($"/orcamentos/{os}");

    public Task<HttpResponseMessage> AprovarAsync(Guid orcamentoId) => _http.PostAsync($"/orcamentos/{orcamentoId}/aprovar", null);

    public Task<HttpResponseMessage> RecusarAsync(Guid orcamentoId) => _http.PostAsync($"/orcamentos/{orcamentoId}/recusar", null);

    public Task<HttpResponseMessage> HealthAsync(string caminho) => _http.GetAsync(caminho);

    /// <summary>Simula o Mercado Pago chamando o webhook (id no formato fake-{status}-{pagamentoId}).</summary>
    public Task<HttpResponseMessage> WebhookAsync(string paymentId, string? idempotencyKey = null, string type = "payment")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/pagamentos/webhook/mercado-pago?data.id={paymentId}&type={type}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { action = "payment.updated", type, data = new { id = paymentId } }),
                Encoding.UTF8, "application/json")
        };
        if (idempotencyKey is not null)
            request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        return _http.SendAsync(request);
    }

    public Task<HttpResponseMessage> WebhookSemIdAsync() =>
        _http.PostAsync("/pagamentos/webhook/mercado-pago", new StringContent("{\"type\":\"payment\"}", Encoding.UTF8, "application/json"));
}
