using System.Net.Http.Json;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// REST síncrono: Billing -> Cadastros Service. Espelha GET /pecas/{id} e
// GET /servicos/{id} (openapi/cadastros.yaml).
public class CadastrosServiceClient : ICadastrosServiceClient
{
    private readonly HttpClient _httpClient;

    public CadastrosServiceClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<PrecoItem?> ObterPrecoPecaAsync(Guid pecaId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/pecas/{pecaId}", ct);
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<PecaDto>(cancellationToken: ct);
        return dto is null ? null : new PrecoItem(dto.Id, dto.Nome, dto.PrecoUnitario);
    }

    public async Task<PrecoItem?> ObterPrecoServicoAsync(Guid servicoId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/servicos/{servicoId}", ct);
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<ServicoDto>(cancellationToken: ct);
        return dto is null ? null : new PrecoItem(dto.Id, dto.Nome, dto.PrecoUnitario);
    }

    // Campos batem com os schemas Peca/Servico do contrato (cadastros.yaml).
    private sealed record PecaDto(Guid Id, string Codigo, string Nome, decimal PrecoUnitario, int Estoque);
    private sealed record ServicoDto(Guid Id, string Nome, decimal PrecoUnitario, decimal TempoEstimadoHoras);
}
