using System.Net.Http.Json;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// Implementação REST síncrona: Billing -> Cadastros Service.
public class CadastrosServiceClient : ICadastrosServiceClient
{
    private readonly HttpClient _httpClient;

    public CadastrosServiceClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<PrecoItem?> ObterPrecoPecaAsync(Guid pecaId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/pecas/{pecaId}", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PrecoItem>(cancellationToken: ct)
            : null;
    }

    public async Task<PrecoItem?> ObterPrecoServicoAsync(Guid servicoId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/servicos/{servicoId}", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PrecoItem>(cancellationToken: ct)
            : null;
    }
}
