using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Messages;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Pagamento.CriarCobranca;

// Disparado pela mensagem "execucao.finalizada": cria a preferência no Mercado Pago e persiste o Pagamento (Pendente).
public class CriarCobrancaUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IMercadoPagoGateway _gateway;
    private readonly IAppLogger<CriarCobrancaUseCase> _logger;

    public CriarCobrancaUseCase(
        IOrcamentoRepository orcamentoRepository,
        IPagamentoRepository pagamentoRepository,
        IMercadoPagoGateway gateway,
        IAppLogger<CriarCobrancaUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _pagamentoRepository = pagamentoRepository;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<Result<PagamentoResponse>> ExecutarAsync(ExecucaoFinalizadaMessage mensagem, CancellationToken ct = default)
    {
        if (mensagem.OrdemServicoId == Guid.Empty)
            return Result<PagamentoResponse>.Falha("OrdemServicoId é obrigatório.");

        var orcamento = await _orcamentoRepository.ObterPorOrdemServicoIdAsync(mensagem.OrdemServicoId, ct);
        if (orcamento is null)
            return Result<PagamentoResponse>.Falha("Orçamento não encontrado para a OS.", TipoErro.NaoEncontrado);

        if (orcamento.Status != StatusOrcamento.Aprovado)
            return Result<PagamentoResponse>.Falha(
                $"Não é possível cobrar um orçamento com status '{orcamento.Status}'.", TipoErro.Conflito);

        var ativo = await _pagamentoRepository.ObterAtivoPorOrdemServicoIdAsync(mensagem.OrdemServicoId, ct);
        if (ativo is not null)
        {
            _logger.LogInformation("Já existe cobrança ativa para a OS {OrdemServicoId}, ignorando duplicata.", mensagem.OrdemServicoId);
            return Result<PagamentoResponse>.Ok(ativo.ParaResponse());
        }

        var pagamento = Domain.Entities.Pagamento.Criar(orcamento.Id, orcamento.OrdemServicoId, orcamento.ValorTotal);

        var cobranca = await _gateway.CriarCobrancaAsync(
            pagamento.Id, pagamento.Valor.Valor, $"Ordem de serviço {orcamento.OrdemServicoId}", ct);

        pagamento.RegistrarCobranca(cobranca.PreferenceId, cobranca.LinkPagamento);

        await _pagamentoRepository.AdicionarAsync(pagamento, ct);
        await _pagamentoRepository.SalvarAlteracoesAsync(ct);

        return Result<PagamentoResponse>.Ok(pagamento.ParaResponse());
    }
}
