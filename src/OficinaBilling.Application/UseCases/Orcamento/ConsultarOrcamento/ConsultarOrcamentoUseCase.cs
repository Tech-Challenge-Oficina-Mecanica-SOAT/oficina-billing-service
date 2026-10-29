using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Orcamento.ConsultarOrcamento;

// GET /orcamentos/{osId}
public class ConsultarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;

    public ConsultarOrcamentoUseCase(IOrcamentoRepository orcamentoRepository) =>
        _orcamentoRepository = orcamentoRepository;

    public async Task<Result<OrcamentoResponse>> ExecutarAsync(Guid ordemServicoId, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorOrdemServicoIdAsync(ordemServicoId, ct);
        return orcamento is null
            ? Result<OrcamentoResponse>.Falha("Orçamento não encontrado para a OS informada.", TipoErro.NaoEncontrado)
            : Result<OrcamentoResponse>.Ok(orcamento.ParaResponse());
    }
}
