using OficinaBilling.Application.Common;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Pagamento.ProcessarWebhookMercadoPago;

// Chamado pelo endpoint de webhook. Pontos de atenção:
//   1. Idempotência — o MP reenvia a mesma notificação várias vezes (estado do domínio + store de chaves).
//   2. Nunca confiar no payload — o status real é SEMPRE consultado na API do MP pelo ID.
//   3. Confere o valor pago contra o valor cobrado antes de confirmar.
public class ProcessarWebhookMercadoPagoUseCase
{
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IMercadoPagoGateway _gateway;
    private readonly IOutbox _outbox;
    private readonly IAppLogger<ProcessarWebhookMercadoPagoUseCase> _logger;

    public ProcessarWebhookMercadoPagoUseCase(
        IPagamentoRepository pagamentoRepository,
        IMercadoPagoGateway gateway,
        IOutbox outbox,
        IAppLogger<ProcessarWebhookMercadoPagoUseCase> logger)
    {
        _pagamentoRepository = pagamentoRepository;
        _gateway = gateway;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecutarAsync(string mercadoPagoPaymentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mercadoPagoPaymentId))
            return Result<bool>.Falha("Id do pagamento é obrigatório.");

        var mp = await _gateway.ObterPagamentoAsync(mercadoPagoPaymentId, ct);
        if (mp is null)
        {
            _logger.LogWarning("Pagamento {PaymentId} não existe no Mercado Pago.", mercadoPagoPaymentId);
            return Result<bool>.Falha("Pagamento não encontrado no Mercado Pago.", TipoErro.NaoEncontrado);
        }

        Domain.Entities.Pagamento? pagamento = null;
        if (Guid.TryParse(mp.ExternalReference, out var pagamentoId))
            pagamento = await _pagamentoRepository.ObterPorIdAsync(pagamentoId, ct);
        pagamento ??= await _pagamentoRepository.ObterPorMercadoPagoIdAsync(mercadoPagoPaymentId, ct);

        if (pagamento is null)
        {
            _logger.LogWarning("Webhook para pagamento desconhecido: {PaymentId} (external_reference={Ref})",
                mercadoPagoPaymentId, mp.ExternalReference ?? "-");
            return Result<bool>.Falha("Pagamento não encontrado.", TipoErro.NaoEncontrado);
        }

        var aprovado = mp.Status == "approved";

        if (aprovado && pagamento.Status is StatusPagamento.Cancelado or StatusPagamento.Recusado)
        {
            // Dinheiro entrou para uma cobrança que o Saga já encerrou: devolve ao cliente.
            _logger.LogWarning("Pagamento {PaymentId} aprovado após a cobrança {PagamentoId} estar {Status}. Estornando.",
                mercadoPagoPaymentId, pagamento.Id, pagamento.Status);
            try
            {
                await _gateway.EstornarAsync(mercadoPagoPaymentId, $"estorno-tardio-{pagamento.Id}", ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao estornar o pagamento tardio {PaymentId}. Requer conciliação manual.", mercadoPagoPaymentId);
                throw;
            }
            return Result<bool>.Ok(true);
        }

        if (aprovado && mp.Valor != pagamento.Valor.Valor)
        {
            _logger.LogError(new InvalidOperationException("Valor divergente"), "Valor divergente no pagamento {PaymentId}: MP={ValorMp} cobrado={ValorCobrado}. Não confirmado.",
                mercadoPagoPaymentId, mp.Valor, pagamento.Valor.Valor);
            return Result<bool>.Falha("Valor pago diverge do valor cobrado.", TipoErro.Conflito);
        }

        pagamento.RegistrarPagamentoMercadoPago(mercadoPagoPaymentId);

        switch (mp.Status)
        {
            case "approved":
                pagamento.Confirmar(); // idempotente: segunda chamada não gera evento
                break;
            case "rejected":
            case "cancelled":
                pagamento.Recusar();
                break;
            case "refunded":
            case "charged_back":
                pagamento.Estornar();
                break;
            default: // pending, in_process, authorized, in_mediation: aguardando
                break;
        }

        _outbox.EnfileirarEventos(pagamento);
        await _pagamentoRepository.SalvarAlteracoesAsync(ct);

        return Result<bool>.Ok(true);
    }
}
