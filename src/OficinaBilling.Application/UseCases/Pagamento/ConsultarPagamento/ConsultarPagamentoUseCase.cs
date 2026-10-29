using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Pagamento.ConsultarPagamento;

// GET /pagamentos/{osId} — devolve também o link de pagamento do Mercado Pago.
public class ConsultarPagamentoUseCase
{
    private readonly IPagamentoRepository _pagamentoRepository;

    public ConsultarPagamentoUseCase(IPagamentoRepository pagamentoRepository) =>
        _pagamentoRepository = pagamentoRepository;

    public async Task<Result<PagamentoResponse>> ExecutarAsync(Guid ordemServicoId, CancellationToken ct = default)
    {
        var pagamento = await _pagamentoRepository.ObterUltimoPorOrdemServicoIdAsync(ordemServicoId, ct);
        return pagamento is null
            ? Result<PagamentoResponse>.Falha("Pagamento não encontrado para a OS informada.", TipoErro.NaoEncontrado)
            : Result<PagamentoResponse>.Ok(pagamento.ParaResponse());
    }
}
