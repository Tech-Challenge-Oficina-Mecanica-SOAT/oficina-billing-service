using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Orcamento;

// POST /orcamentos/{orcamentoId}/recusar — publica "orcamento.recusado".
// O OS Service reage a esse evento disparando "os.cancelada" (não é o Billing quem cancela a OS).
public class RecusarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;

    public RecusarOrcamentoUseCase(IOrcamentoRepository orcamentoRepository, IEventPublisher eventPublisher, IUnitOfWork unitOfWork)
    {
        _orcamentoRepository = orcamentoRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<OrcamentoResponse>> ExecutarAsync(Guid orcamentoId, RecusarOrcamentoRequest request, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorIdAsync(orcamentoId, ct);
        if (orcamento is null)
            return Result<OrcamentoResponse>.Falha("Orçamento não encontrado.");

        try
        {
            orcamento.Recusar(request.Motivo);
        }
        catch (InvalidOperationException ex)
        {
            return Result<OrcamentoResponse>.Falha(ex.Message);
        }

        foreach (var domainEvent in orcamento.DomainEvents)
            await _eventPublisher.EnfileirarAsync(domainEvent, ct);

        await _unitOfWork.SalvarAlteracoesAsync(ct);

        return Result<OrcamentoResponse>.Ok(orcamento.ParaResponse());
    }
}
