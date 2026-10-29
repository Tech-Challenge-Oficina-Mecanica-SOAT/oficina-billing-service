namespace OficinaBilling.Application.Interfaces;

// Porta para chamada REST síncrona: Billing -> Cadastros Service.
public interface ICadastrosServiceClient
{
    Task<PrecoItem?> ObterPrecoPecaAsync(Guid pecaId, CancellationToken ct = default);
    Task<PrecoItem?> ObterPrecoServicoAsync(Guid servicoId, CancellationToken ct = default);
}

public sealed record PrecoItem(Guid Id, string Descricao, decimal PrecoUnitario);
