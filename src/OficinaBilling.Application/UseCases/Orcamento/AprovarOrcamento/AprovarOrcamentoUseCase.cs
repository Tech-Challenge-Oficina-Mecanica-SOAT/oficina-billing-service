using OficinaBilling.Application.Common;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Orcamento.AprovarOrcamento;

// TODO: chamado pelo endpoint POST /orcamentos/{id}/aprovar (cliente aprova via API).
public class AprovarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IEventPublisher _eventPublisher;

    public AprovarOrcamentoUseCase(IOrcamentoRepository orcamentoRepository, IEventPublisher eventPublisher)
    {
        _orcamentoRepository = orcamentoRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<Result<bool>> ExecutarAsync(Guid orcamentoId, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorIdAsync(orcamentoId, ct);
        if (orcamento is null)
            return Result<bool>.Falha("Orçamento não encontrado.");

        orcamento.Aprovar();
        await _orcamentoRepository.SalvarAlteracoesAsync(ct);

        foreach (var domainEvent in orcamento.DomainEvents)
            await _eventPublisher.PublicarAsync(domainEvent, ct);

        return Result<bool>.Ok(true);
    }
}
