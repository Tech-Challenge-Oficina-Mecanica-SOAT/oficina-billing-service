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
}
