using FluentAssertions;
using System.Net;
using System.Text;
using OficinaBilling.Infrastructure.ExternalServices;

namespace OficinaBilling.UnitTests.ExternalServices;

// Payloads no formato REAL devolvido pelo Cadastros (campo "nome", sem "descricao").
public class CadastrosServiceClientTests
{
    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private static CadastrosServiceClient Criar(string json) =>
        new(new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("http://cadastros") });

    [Fact]
    public async Task Peca_com_payload_real_do_Cadastros_le_nome_como_descricao()
    {
        var id = Guid.NewGuid();
        var client = Criar($"{{\"id\":\"{id}\",\"codigo\":\"P-1\",\"nome\":\"Pastilha\",\"precoUnitario\":150.00,\"estoque\":10}}");

        var item = await client.ObterPrecoPecaAsync(id);

        item.Should().NotBeNull();
        item!.Id.Should().Be(id);
        item.Descricao.Should().Be("Pastilha");
        item.PrecoUnitario.Should().Be(150.00m);
    }

    [Fact]
    public async Task Servico_com_payload_real_do_Cadastros_le_nome_como_descricao()
    {
        var id = Guid.NewGuid();
        var client = Criar($"{{\"id\":\"{id}\",\"nome\":\"Troca\",\"precoUnitario\":200.00,\"tempoEstimadoHoras\":1.5}}");

        var item = await client.ObterPrecoServicoAsync(id);

        item.Should().NotBeNull();
        item!.Descricao.Should().Be("Troca");
        item.PrecoUnitario.Should().Be(200.00m);
    }

    [Fact]
    public async Task Chave_antiga_descricao_continua_aceita_como_fallback()
    {
        var id = Guid.NewGuid();
        var client = Criar($"{{\"id\":\"{id}\",\"descricao\":\"Legado\",\"precoUnitario\":1.50}}");

        var item = await client.ObterPrecoPecaAsync(id);

        item!.Descricao.Should().Be("Legado");
    }

    [Fact]
    public async Task Nome_tem_prioridade_sobre_descricao()
    {
        var id = Guid.NewGuid();
        var client = Criar($"{{\"id\":\"{id}\",\"nome\":\"Novo\",\"descricao\":\"Velho\",\"precoUnitario\":1}}");

        (await client.ObterPrecoPecaAsync(id))!.Descricao.Should().Be("Novo");
    }

    private sealed class CapturingHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public CancellationToken Token { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uri = request.RequestUri;
            Token = ct;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static CadastrosServiceClient Criar(CapturingHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cadastros") });

    [Fact]
    public async Task Peca_inexistente_404_retorna_null_e_chama_rota_de_pecas()
    {
        var handler = new CapturingHandler(HttpStatusCode.NotFound);
        var id = Guid.NewGuid();

        var item = await Criar(handler).ObterPrecoPecaAsync(id);

        item.Should().BeNull();
        handler.Uri!.AbsolutePath.Should().Be($"/pecas/{id}");
    }

    [Fact]
    public async Task Servico_inexistente_404_retorna_null_e_chama_rota_de_servicos()
    {
        var handler = new CapturingHandler(HttpStatusCode.NotFound);
        var id = Guid.NewGuid();

        var item = await Criar(handler).ObterPrecoServicoAsync(id);

        item.Should().BeNull();
        handler.Uri!.AbsolutePath.Should().Be($"/servicos/{id}");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Falha_diferente_de_404_lanca_para_reprocessar(HttpStatusCode status)
    {
        var client = Criar(new CapturingHandler(status));

        var peca = () => client.ObterPrecoPecaAsync(Guid.NewGuid());
        var servico = () => client.ObterPrecoServicoAsync(Guid.NewGuid());

        await peca.Should().ThrowAsync<HttpRequestException>();
        await servico.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Corpo_json_null_retorna_null()
    {
        var item = await Criar(new CapturingHandler(HttpStatusCode.OK, "null")).ObterPrecoPecaAsync(Guid.NewGuid());

        item.Should().BeNull();
    }

    [Fact]
    public async Task Sem_nome_e_sem_descricao_usa_string_vazia()
    {
        var id = Guid.NewGuid();
        var client = Criar($"{{\"id\":\"{id}\",\"precoUnitario\":9.90}}");

        var item = await client.ObterPrecoServicoAsync(id);

        item!.Descricao.Should().BeEmpty();
        item.PrecoUnitario.Should().Be(9.90m);
    }

    [Fact]
    public async Task Cancellation_token_cancelado_interrompe_a_chamada()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, "{}");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var chamada = () => Criar(handler).ObterPrecoPecaAsync(Guid.NewGuid(), cts.Token);

        await chamada.Should().ThrowAsync<OperationCanceledException>();
    }
}
