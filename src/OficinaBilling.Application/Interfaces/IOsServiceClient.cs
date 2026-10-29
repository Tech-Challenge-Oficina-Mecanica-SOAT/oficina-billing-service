namespace OficinaBilling.Application.Interfaces;

// Porta para chamada REST síncrona: Billing -> OS Service.
// Implementação concreta (HttpClient) fica em Infrastructure/ExternalServices.
public interface IOsServiceClient
{
    Task<OrdemServicoResumo?> ObterOrdemServicoAsync(Guid ordemServicoId, CancellationToken ct = default);
}

public sealed record OrdemServicoResumo(Guid Id, string Status, Guid ClienteId);
