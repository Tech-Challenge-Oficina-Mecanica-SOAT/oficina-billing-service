namespace OficinaBilling.Application.Interfaces;

// Porta REST síncrona: Billing -> Cadastros Service.
// Espelha GET /pecas/{id} e GET /servicos/{id} do contrato openapi/cadastros.yaml.
public interface ICadastrosServiceClient
{
    Task<PrecoItem?> ObterPrecoPecaAsync(Guid pecaId, CancellationToken ct = default);
    Task<PrecoItem?> ObterPrecoServicoAsync(Guid servicoId, CancellationToken ct = default);
}

public sealed record PrecoItem(Guid Id, string Nome, decimal PrecoUnitario);
