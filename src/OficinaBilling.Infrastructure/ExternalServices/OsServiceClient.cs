using System.Net.Http.Json;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// Implementação REST síncrona: Billing -> OS Service.
// A base URL vem do Service K8s do OS Service (DNS interno do cluster),
// injetada via ConfigMap — ver k8s/configmap.yaml.
public class OsServiceClient : IOsServiceClient
{
    private readonly HttpClient _httpClient;

    public OsServiceClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<OrdemServicoResumo?> ObterOrdemServicoAsync(Guid ordemServicoId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/ordens-servico/{ordemServicoId}", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<OrdemServicoResumo>(cancellationToken: ct);
    }
}
