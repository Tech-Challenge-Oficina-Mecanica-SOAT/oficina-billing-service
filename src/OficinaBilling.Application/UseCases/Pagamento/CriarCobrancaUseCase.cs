using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Interfaces;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Application.UseCases.Pagamento;

// Disparado pelo consumer SQS que escuta "execucao.finalizada" (Execução Service).
// Cria a preferência de pagamento no Mercado Pago e persiste o Pagamento como Pendente.
// A confirmação real vem depois, via webhook (ProcessarWebhookMercadoPagoUseCase).
public class CriarCobrancaUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IMercadoPagoGateway _mercadoPagoGateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppLogger<CriarCobrancaUseCase> _logger;

    public CriarCobrancaUseCase(
        IOrcamentoRepository orcamentoRepository,
        IPagamentoRepository pagamentoRepository,
        IMercadoPagoGateway mercadoPagoGateway,
        IUnitOfWork unitOfWork,
        IAppLogger<CriarCobrancaUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _pagamentoRepository = pagamentoRepository;
        _mercadoPagoGateway = mercadoPagoGateway;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PagamentoResponse>> ExecutarAsync(Guid osId, CancellationToken ct = default)
    {
        // Idempotência: "execucao.finalizada" pode ser reentregue pelo SQS.
        var existente = await _pagamentoRepository.ObterPorOsIdAsync(osId, ct);
        if (existente is not null)
        {
            _logger.LogInformation("Pagamento já existe para a OS {OsId}, ignorando duplicata.", osId);
            return Result<PagamentoResponse>.Ok(existente.ParaResponse());
        }

        var orcamento = await _orcamentoRepository.ObterPorOsIdAsync(osId, ct);
        if (orcamento is null)
            return Result<PagamentoResponse>.Falha($"Nenhum orçamento encontrado para a OS {osId}.");

        // Sem essa checagem dava para cobrar orçamento Gerado, Recusado, Expirado ou Cancelado.
        if (orcamento.Status != StatusOrcamento.Aprovado)
            return Result<PagamentoResponse>.Falha(
                $"O orçamento da OS {osId} está '{orcamento.Status}'; só é possível cobrar orçamento 'Aprovado'.");

        var preferencia = await _mercadoPagoGateway.CriarPreferenciaAsync(
            orcamento.ValorTotal.Valor,
            $"Oficina Mecânica - OS {osId}",
            orcamento.Id,
            ct);

        var pagamento = Domain.Entities.Pagamento.CriarCobranca(
            osId, orcamento.Id, new Dinheiro(orcamento.ValorTotal.Valor), preferencia.PreferenceId, preferencia.LinkPagamento);

        await _pagamentoRepository.AdicionarAsync(pagamento, ct);
        await _unitOfWork.SalvarAlteracoesAsync(ct);

        return Result<PagamentoResponse>.Ok(pagamento.ParaResponse());
    }
}
