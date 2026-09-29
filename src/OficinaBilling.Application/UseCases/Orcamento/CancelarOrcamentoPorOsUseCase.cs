using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Orcamento;

// Disparado pelo consumer que escuta "os.cancelada" (compensação da saga).
// Não publica evento novo: é uma reação a um evento, não um fato de negócio novo.
public class CancelarOrcamentoPorOsUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Interfaces.IAppLogger<CancelarOrcamentoPorOsUseCase> _logger;

    public CancelarOrcamentoPorOsUseCase(IOrcamentoRepository orcamentoRepository, IUnitOfWork unitOfWork, Interfaces.IAppLogger<CancelarOrcamentoPorOsUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ExecutarAsync(Guid osId, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorOsIdAsync(osId, ct);
        if (orcamento is null)
        {
            _logger.LogInformation("os.cancelada recebido para OS {OsId} sem orçamento associado — nada a fazer.", osId);
            return;
        }

        if (orcamento.CancelarPorCompensacao())
            await _unitOfWork.SalvarAlteracoesAsync(ct);
    }
}
