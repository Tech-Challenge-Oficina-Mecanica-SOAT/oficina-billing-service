using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Pagamento;

// POST /pagamentos/webhook/mercado-pago. Ponto de maior atenção do serviço:
//   1. Idempotência via X-Idempotency-Key — o MP reenvia a mesma notificação várias vezes.
//   2. Nunca confie cegamente no payload — sempre confirme o status consultando
//      a API do Mercado Pago pelo ID (evita webhook forjado).
//   3. Sempre responder 200 rápido pro MP, mesmo em erro de negócio (ver Controller) —
//      isso é tratado aqui devolvendo Result.Falha em vez de lançar exceção.
public class ProcessarWebhookMercadoPagoUseCase
{
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IMercadoPagoGateway _mercadoPagoGateway;
    private readonly IEventPublisher _eventPublisher;
    private readonly IIdempotenciaRepository _idempotenciaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppLogger<ProcessarWebhookMercadoPagoUseCase> _logger;

    public ProcessarWebhookMercadoPagoUseCase(
        IPagamentoRepository pagamentoRepository,
        IMercadoPagoGateway mercadoPagoGateway,
        IEventPublisher eventPublisher,
        IIdempotenciaRepository idempotenciaRepository,
        IUnitOfWork unitOfWork,
        IAppLogger<ProcessarWebhookMercadoPagoUseCase> logger)
    {
        _pagamentoRepository = pagamentoRepository;
        _mercadoPagoGateway = mercadoPagoGateway;
        _eventPublisher = eventPublisher;
        _idempotenciaRepository = idempotenciaRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecutarAsync(WebhookMercadoPagoRequest request, CancellationToken ct = default)
    {
        if (await _idempotenciaRepository.JaProcessadoAsync(request.IdempotencyKey, ct))
        {
            _logger.LogInformation("Webhook {IdempotencyKey} já processado, ignorando.", request.IdempotencyKey);
            return Result<bool>.Ok(true);
        }

        // Confirma o status real na API do MP em vez de confiar no payload do webhook.
        var statusReal = await _mercadoPagoGateway.ConsultarPagamentoAsync(request.MercadoPagoPaymentId, ct);

        // O webhook do MP manda o "payment id", mas o pagamento aqui foi criado com o
        // "preference id". Na prática, ao consultar o payment na API do MP (ConsultarPagamentoAsync)
        // o retorno traz a preferência/external_reference associada — ajuste esta busca para usar
        // esse dado real assim que o MercadoPagoGateway estiver implementado de verdade.
        var pagamento = await _pagamentoRepository.ObterPorPreferenceIdAsync(request.MercadoPagoPaymentId, ct);

        if (pagamento is null)
        {
            _logger.LogWarning("Webhook recebido para pagamento desconhecido: {PaymentId}", request.MercadoPagoPaymentId);
            return Result<bool>.Falha("Pagamento não encontrado.");
        }

        var houveTransicao = statusReal.Status switch
        {
            "approved" => pagamento.Confirmar(statusReal.MercadoPagoPaymentId),
            "rejected" or "cancelled" => pagamento.Recusar(statusReal.MercadoPagoPaymentId, statusReal.Status),
            _ => false // pending, in_process etc. — nada a fazer ainda
        };

        if (houveTransicao)
        {
            foreach (var domainEvent in pagamento.DomainEvents)
                await _eventPublisher.EnfileirarAsync(domainEvent, ct);
        }

        await _idempotenciaRepository.MarcarProcessadoAsync(request.IdempotencyKey, ct);
        await _unitOfWork.SalvarAlteracoesAsync(ct);

        return Result<bool>.Ok(true);
    }
}
