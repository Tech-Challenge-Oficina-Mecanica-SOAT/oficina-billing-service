using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Interfaces;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;

// Disparado pelo handler da mensagem "diagnostico.concluido" (Execução Service).
// Fluxo: busca preço de cada peça/serviço no Cadastros -> soma -> persiste + outbox "orcamento.gerado".
public class GerarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly ICadastrosServiceClient _cadastrosServiceClient;
    private readonly IOutbox _outbox;
    private readonly IAppLogger<GerarOrcamentoUseCase> _logger;

    public GerarOrcamentoUseCase(
        IOrcamentoRepository orcamentoRepository,
        ICadastrosServiceClient cadastrosServiceClient,
        IOutbox outbox,
        IAppLogger<GerarOrcamentoUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _cadastrosServiceClient = cadastrosServiceClient;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<Result<OrcamentoResponse>> ExecutarAsync(GerarOrcamentoRequest request, CancellationToken ct = default)
    {
        if (request.OrdemServicoId == Guid.Empty)
            return Result<OrcamentoResponse>.Falha("OrdemServicoId é obrigatório.");

        var existente = await _orcamentoRepository.ObterPorOrdemServicoIdAsync(request.OrdemServicoId, ct);
        if (existente is not null)
        {
            // idempotência: a mensagem SQS pode ser reentregue
            _logger.LogInformation("Orçamento já existe para a OS {OrdemServicoId}, ignorando duplicata.", request.OrdemServicoId);
            return Result<OrcamentoResponse>.Ok(existente.ParaResponse());
        }

        var pecas = request.Pecas ?? new List<ItemDiagnostico>();
        var servicos = request.Servicos ?? new List<ItemDiagnostico>();

        if (pecas.Count + servicos.Count == 0)
            return Result<OrcamentoResponse>.Falha("O diagnóstico não possui peças nem serviços.");

        if (pecas.Concat(servicos).Any(i => i.Quantidade <= 0))
            return Result<OrcamentoResponse>.Falha("Quantidade deve ser maior que zero.");

        var itens = new List<ItemOrcamento>();

        foreach (var peca in pecas)
        {
            var preco = await _cadastrosServiceClient.ObterPrecoPecaAsync(peca.Id, ct);
            if (preco is null)
                return Result<OrcamentoResponse>.Falha($"Peça {peca.Id} não encontrada no Cadastros Service.", TipoErro.NaoEncontrado);

            itens.Add(new ItemOrcamento(preco.Descricao, peca.Quantidade, new Dinheiro(preco.PrecoUnitario)));
        }

        foreach (var servico in servicos)
        {
            var preco = await _cadastrosServiceClient.ObterPrecoServicoAsync(servico.Id, ct);
            if (preco is null)
                return Result<OrcamentoResponse>.Falha($"Serviço {servico.Id} não encontrado no Cadastros Service.", TipoErro.NaoEncontrado);

            itens.Add(new ItemOrcamento(preco.Descricao, servico.Quantidade, new Dinheiro(preco.PrecoUnitario)));
        }

        var orcamento = Domain.Entities.Orcamento.Gerar(request.OrdemServicoId, itens);

        await _orcamentoRepository.AdicionarAsync(orcamento, ct);
        _outbox.EnfileirarEventos(orcamento);
        await _orcamentoRepository.SalvarAlteracoesAsync(ct); // orçamento + outbox na mesma transação

        return Result<OrcamentoResponse>.Ok(orcamento.ParaResponse());
    }
}
