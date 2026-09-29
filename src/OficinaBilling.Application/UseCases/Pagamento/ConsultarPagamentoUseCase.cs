using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Pagamento;

// GET /pagamentos/{osId}
public class ConsultarPagamentoUseCase
{
    private readonly IPagamentoRepository _pagamentoRepository;

    public ConsultarPagamentoUseCase(IPagamentoRepository pagamentoRepository) => _pagamentoRepository = pagamentoRepository;

    public async Task<Result<PagamentoResponse>> ExecutarAsync(Guid osId, CancellationToken ct = default)
    {
        var pagamento = await _pagamentoRepository.ObterPorOsIdAsync(osId, ct);
        return pagamento is null
            ? Result<PagamentoResponse>.Falha("Pagamento não encontrado para essa OS.")
            : Result<PagamentoResponse>.Ok(pagamento.ParaResponse());
    }
}
