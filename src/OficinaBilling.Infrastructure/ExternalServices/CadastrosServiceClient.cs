using System.Net;
using System.Net.Http.Json;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// REST síncrono Billing -> Cadastros. 404 = "não existe" (null). Qualquer outra falha
// (5xx, timeout) LANÇA, para a mensagem SQS ser reprocessada em vez de virar "peça inexistente".
// Retry/timeout/circuit breaker vêm do handler de resiliência registrado no HttpClient.
public class CadastrosServiceClient : ICadastrosServiceClient
{
    private readonly HttpClient _httpClient;

    public CadastrosServiceClient(HttpClient httpClient) => _httpClient = httpClient;

    public Task<PrecoItem?> ObterPrecoPecaAsync(Guid pecaId, CancellationToken ct = default) =>
        ObterAsync($"/pecas/{pecaId}", ct);

    public Task<PrecoItem?> ObterPrecoServicoAsync(Guid servicoId, CancellationToken ct = default) =>
        ObterAsync($"/servicos/{servicoId}", ct);

    private async Task<PrecoItem?> ObterAsync(string caminho, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(caminho, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<CadastrosItemResponse>(cancellationToken: ct);
        return dto is null ? null : new PrecoItem(dto.Id, dto.Nome ?? dto.Descricao ?? string.Empty, dto.PrecoUnitario);
    }

    // Formato real do Cadastros: "nome". "descricao" fica como fallback de contrato antigo.
    private sealed record CadastrosItemResponse(Guid Id, string? Nome, string? Descricao, decimal PrecoUnitario);
}
