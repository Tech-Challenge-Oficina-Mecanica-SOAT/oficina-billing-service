using OficinaBilling.Application.Common;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Pagamento.ProcessarWebhookMercadoPago;

// Chamado pelo WebhooksController quando o Mercado Pago notifica uma mudança
// de status de pagamento. Ponto de maior atenção do serviço:
//   1. Idempotência — o MP pode reenviar a mesma notificação várias vezes.
//   2. Nunca confie cegamente no payload — sempre confirme o status
//      consultando a API do Mercado Pago pelo ID (evita webhook forjado).
public class ProcessarWebhookMercadoPagoUseCase
{
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IMercadoPagoGateway _mercadoPagoGateway;
    private readonly IEventPublisher _eventPublisher;
    private readonly IAppLogger<ProcessarWebhookMercadoPagoUseCase> _logger;

    public ProcessarWebhookMercadoPagoUseCase(
        IPagamentoRepository pagamentoRepository,
        IMercadoPagoGateway mercadoPagoGateway,
        IEventPublisher eventPublisher,
        IAppLogger<ProcessarWebhookMercadoPagoUseCase> logger)
    {
        _pagamentoRepository = pagamentoRepository;
        _mercadoPagoGateway = mercadoPagoGateway;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecutarAsync(string mercadoPagoPaymentId, CancellationToken ct = default)
    {
        var pagamento = await _pagamentoRepository.ObterPorMercadoPagoIdAsync(mercadoPagoPaymentId, ct);
        if (pagamento is null)
        {
            _logger.LogWarning("Webhook recebido para pagamento desconhecido: {PaymentId}", mercadoPagoPaymentId);
            return Result<bool>.Falha("Pagamento não encontrado.");
        }

        // TODO: chamar _mercadoPagoGateway para confirmar o status real (não confiar só no webhook)
        pagamento.Confirmar(); // idempotente: chamar duas vezes não gera dois eventos

        await _pagamentoRepository.SalvarAlteracoesAsync(ct);

        foreach (var domainEvent in pagamento.DomainEvents)
            await _eventPublisher.PublicarAsync(domainEvent, ct);

        return Result<bool>.Ok(true);
    }
}
