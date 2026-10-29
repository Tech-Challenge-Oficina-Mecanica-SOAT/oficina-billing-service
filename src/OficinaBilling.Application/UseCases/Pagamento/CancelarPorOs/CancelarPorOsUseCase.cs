using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Messages;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Pagamento.CancelarPorOs;

// Compensação do Saga: o OS Service publicou "os.cancelada".
//   orçamento Gerado/Aprovado -> Cancelado
//   pagamento Pendente        -> Cancelado
//   pagamento Confirmado      -> estorno no Mercado Pago + Estornado
public class CancelarPorOsUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IMercadoPagoGateway _gateway;
    private readonly IAppLogger<CancelarPorOsUseCase> _logger;

    public CancelarPorOsUseCase(
        IOrcamentoRepository orcamentoRepository,
        IPagamentoRepository pagamentoRepository,
        IMercadoPagoGateway gateway,
        IAppLogger<CancelarPorOsUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _pagamentoRepository = pagamentoRepository;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecutarAsync(OsCanceladaMessage mensagem, CancellationToken ct = default)
    {
        if (mensagem.OrdemServicoId == Guid.Empty)
            return Result<bool>.Falha("OrdemServicoId é obrigatório.");

        var orcamento = await _orcamentoRepository.ObterPorOrdemServicoIdAsync(mensagem.OrdemServicoId, ct);
        var pagamentos = await _pagamentoRepository.ListarPorOrdemServicoIdAsync(mensagem.OrdemServicoId, ct);

        if (orcamento is null && pagamentos.Count == 0)
        {
            _logger.LogInformation("os.cancelada para OS {OrdemServicoId} sem orçamento/pagamento: nada a compensar.", mensagem.OrdemServicoId);
            return Result<bool>.Ok(true);
        }

        orcamento?.Cancelar();

        foreach (var pagamento in pagamentos)
        {
            if (pagamento.Status == StatusPagamento.Confirmado)
            {
                if (string.IsNullOrEmpty(pagamento.MercadoPagoPaymentId))
                    return Result<bool>.Falha($"Pagamento {pagamento.Id} confirmado sem id do MP: estorno manual necessário.", TipoErro.Conflito);

                await _gateway.EstornarAsync(pagamento.MercadoPagoPaymentId, $"estorno-{pagamento.Id}", ct);
                pagamento.Estornar();
            }
            else
            {
                pagamento.Cancelar();
            }
        }

        // Um único SaveChanges (mesmo DbContext) persiste orçamento e pagamentos.
        if (orcamento is not null)
            await _orcamentoRepository.SalvarAlteracoesAsync(ct);
        else
            await _pagamentoRepository.SalvarAlteracoesAsync(ct);

        return Result<bool>.Ok(true);
    }
}
