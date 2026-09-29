namespace OficinaBilling.Application.Interfaces;

// Porta REST síncrona: Billing -> OS Service.
// Espelha GET /ordens-servico/{id} do contrato openapi/os.yaml.
public interface IOsServiceClient
{
    Task<OrdemServicoResumo?> ObterOrdemServicoAsync(Guid osId, CancellationToken ct = default);
}

public sealed record OrdemServicoResumo(Guid Id, string Status, Guid ClienteId);
