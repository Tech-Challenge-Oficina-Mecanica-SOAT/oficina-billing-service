using System.Net;
using System.Net.Http.Json;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// REST síncrono Billing -> OS Service (GET /ordens-servico/{id}).
// A base URL vem do ConfigMap (DNS interno do cluster) — ver k8s/base/configmap.yaml.
public class OsServiceClient : IOsServiceClient
{
    private readonly HttpClient _httpClient;

    public OsServiceClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<OrdemServicoResumo?> ObterOrdemServicoAsync(Guid ordemServicoId, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync($"/ordens-servico/{ordemServicoId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrdemServicoResumo>(cancellationToken: ct);
    }
}
