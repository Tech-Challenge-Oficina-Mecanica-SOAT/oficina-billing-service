using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Orcamento.AprovarOrcamento;

// POST /orcamentos/{orcamentoId}/aprovar — o cliente aprova; "orcamento.aprovado" dispara a execução.
public class AprovarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IOutbox _outbox;

    public AprovarOrcamentoUseCase(IOrcamentoRepository orcamentoRepository, IOutbox outbox)
    {
        _orcamentoRepository = orcamentoRepository;
        _outbox = outbox;
    }

    public async Task<Result<OrcamentoResponse>> ExecutarAsync(Guid orcamentoId, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorIdAsync(orcamentoId, ct);
        if (orcamento is null)
            return Result<OrcamentoResponse>.Falha("Orçamento não encontrado.", TipoErro.NaoEncontrado);

        if (orcamento.Status == StatusOrcamento.Aprovado)
            return Result<OrcamentoResponse>.Ok(orcamento.ParaResponse()); // idempotente: não republica

        try
        {
            orcamento.Aprovar();
        }
        catch (InvalidOperationException ex)
        {
            return Result<OrcamentoResponse>.Falha(ex.Message, TipoErro.Conflito);
        }

        _outbox.EnfileirarEventos(orcamento);
        await _orcamentoRepository.SalvarAlteracoesAsync(ct);

        return Result<OrcamentoResponse>.Ok(orcamento.ParaResponse());
    }
}
