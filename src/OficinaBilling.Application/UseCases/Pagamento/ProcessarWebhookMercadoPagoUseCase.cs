using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Pagamento;

// POST /pagamentos/webhook/mercado-pago. Ponto de maior atenção do serviço:
//   1. Nunca confie cegamente no payload: sempre confirme o status consultando a API do
//      Mercado Pago pelo payment id (evita webhook forjado).
//   2. O elo entre "payment id" e o nosso Pagamento é o external_reference (= Id do Orçamento),
//      enviado ao criar a preferência. O payment id só existe DEPOIS que o cliente paga.
//   3. Idempotência: chave "mp:{paymentId}:{status}". O MP reenvia a mesma notificação várias
//      vezes, e um mesmo payment muda de status ao longo do tempo (pending -> approved), por
//      isso o status faz parte da chave. Pagamento.Confirmar/Recusar já são idempotentes no
//      domínio (segunda barreira).
//   4. Conferimos o valor: aprovar um pagamento com valor diferente do cobrado seria grave.
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
        var statusReal = await _mercadoPagoGateway.ConsultarPagamentoAsync(request.MercadoPagoPaymentId, ct);
        if (statusReal is null)
        {
            _logger.LogWarning("Webhook para payment {PaymentId} que não existe no Mercado Pago.", request.MercadoPagoPaymentId);
            return Result<bool>.Falha("Payment não encontrado no Mercado Pago.");
        }

        var chave = $"mp:{statusReal.MercadoPagoPaymentId}:{statusReal.Status}";
        if (await _idempotenciaRepository.JaProcessadoAsync(chave, ct))
        {
            _logger.LogInformation("Webhook {Chave} já processado, ignorando.", chave);
            return Result<bool>.Ok(true);
        }

        if (!Guid.TryParse(statusReal.ExternalReference, out var orcamentoId))
        {
            _logger.LogWarning("Payment {PaymentId} sem external_reference válido ('{Ref}'): não é uma cobrança deste serviço.",
                statusReal.MercadoPagoPaymentId, statusReal.ExternalReference ?? "");
            return Result<bool>.Falha("Payment sem external_reference válido.");
        }

        var pagamento = await _pagamentoRepository.ObterPorOrcamentoIdAsync(orcamentoId, ct);
        if (pagamento is null)
        {
            _logger.LogWarning("Webhook para payment {PaymentId} sem Pagamento correspondente (orçamento {OrcamentoId}).",
                statusReal.MercadoPagoPaymentId, orcamentoId);
            return Result<bool>.Falha("Pagamento não encontrado para o external_reference informado.");
        }

        if (statusReal.Status == "approved" && Math.Round(statusReal.Valor, 2) != pagamento.Valor.Valor)
        {
            _logger.LogWarning("Payment {PaymentId} aprovado com valor {ValorPago}, mas a cobrança era {ValorCobrado}: NÃO confirmado.",
                statusReal.MercadoPagoPaymentId, statusReal.Valor, pagamento.Valor.Valor);
            return Result<bool>.Falha("Valor pago diferente do valor cobrado.");
        }

        var houveTransicao = statusReal.Status switch
        {
            "approved" => pagamento.Confirmar(statusReal.MercadoPagoPaymentId),
            "rejected" or "cancelled" => pagamento.Recusar(statusReal.MercadoPagoPaymentId, statusReal.Status),
            _ => false // pending, in_process, refunded etc.: nada a fazer (por enquanto)
        };

        if (houveTransicao)
        {
            foreach (var domainEvent in pagamento.DomainEvents)
                await _eventPublisher.EnfileirarAsync(domainEvent, ct);
        }

        // Pagamento + outbox + chave de idempotência são confirmados juntos, no mesmo SaveChanges.
        await _idempotenciaRepository.MarcarProcessadoAsync(chave, ct);
        await _unitOfWork.SalvarAlteracoesAsync(ct);

        return Result<bool>.Ok(true);
    }
}
