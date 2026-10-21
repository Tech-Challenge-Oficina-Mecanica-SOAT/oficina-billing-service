using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Orcamento;

// POST /orcamentos/{orcamentoId}/aprovar — publica "orcamento.aprovado".
public class AprovarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;

    public AprovarOrcamentoUseCase(IOrcamentoRepository orcamentoRepository, IEventPublisher eventPublisher, IUnitOfWork unitOfWork)
    {
        _orcamentoRepository = orcamentoRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<OrcamentoResponse>> ExecutarAsync(Guid orcamentoId, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorIdAsync(orcamentoId, ct);
        if (orcamento is null)
            return Result<OrcamentoResponse>.Falha("Orçamento não encontrado.");

        try
        {
            orcamento.Aprovar();
        }
        catch (InvalidOperationException ex)
        {
            // Orcamento.Aprovar() marca o status como Expirado antes de lançar; sem salvar,
            // essa transição se perdia e o orçamento continuava "Gerado" no banco.
            if (orcamento.Status == StatusOrcamento.Expirado)
                await _unitOfWork.SalvarAlteracoesAsync(ct);

            return Result<OrcamentoResponse>.Falha(ex.Message);
        }

        foreach (var domainEvent in orcamento.DomainEvents)
            await _eventPublisher.EnfileirarAsync(domainEvent, ct);

        await _unitOfWork.SalvarAlteracoesAsync(ct);

        return Result<OrcamentoResponse>.Ok(orcamento.ParaResponse());
    }
}
