using System.Net.Http.Json;
using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// REST síncrono: Billing -> OS Service. Espelha GET /ordens-servico/{id}
// (openapi/os.yaml). Base URL vem do Service K8s do OS (DNS interno do
// cluster), injetada via ConfigMap — ver k8s/configmap.yaml.
public class OsServiceClient : IOsServiceClient
{
    private readonly HttpClient _httpClient;

    public OsServiceClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<OrdemServicoResumo?> ObterOrdemServicoAsync(Guid osId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/ordens-servico/{osId}", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var dto = await response.Content.ReadFromJsonAsync<OsDetalhadaDto>(cancellationToken: ct);
        return dto is null ? null : new OrdemServicoResumo(dto.Id, dto.Status, dto.ClienteId);
    }

    // Formato mínimo do schema OSDetalhada (openapi/os.yaml) — só os campos que o Billing usa.
    private sealed record OsDetalhadaDto(Guid Id, string Status, Guid ClienteId);
}
