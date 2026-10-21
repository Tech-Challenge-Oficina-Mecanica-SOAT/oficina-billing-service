using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Orcamento;

// GET /orcamentos/{osId} — leitura simples por OS (é assim que o contrato define, não por orcamentoId).
public class ConsultarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;

    public ConsultarOrcamentoUseCase(IOrcamentoRepository orcamentoRepository) => _orcamentoRepository = orcamentoRepository;

    public async Task<Result<OrcamentoResponse>> ExecutarAsync(Guid osId, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorOsIdAsync(osId, ct);
        return orcamento is null
            ? Result<OrcamentoResponse>.Falha("Orçamento não encontrado para essa OS.")
            : Result<OrcamentoResponse>.Ok(orcamento.ParaResponse());
    }
}
