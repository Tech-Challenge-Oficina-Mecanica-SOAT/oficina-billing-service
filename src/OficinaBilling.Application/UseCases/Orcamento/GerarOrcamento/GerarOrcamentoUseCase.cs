using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Interfaces;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;

// Disparado pelo consumer SQS que escuta "diagnostico.concluido" (Execução Service).
// Fluxo: busca preço de cada peça/serviço no Cadastros -> soma -> persiste -> publica "orcamento.gerado".
public class GerarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly ICadastrosServiceClient _cadastrosServiceClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IAppLogger<GerarOrcamentoUseCase> _logger;

    public GerarOrcamentoUseCase(
        IOrcamentoRepository orcamentoRepository,
        ICadastrosServiceClient cadastrosServiceClient,
        IEventPublisher eventPublisher,
        IAppLogger<GerarOrcamentoUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _cadastrosServiceClient = cadastrosServiceClient;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<Result<OrcamentoResponse>> ExecutarAsync(GerarOrcamentoRequest request, CancellationToken ct = default)
    {
        var existente = await _orcamentoRepository.ObterPorOrdemServicoIdAsync(request.OrdemServicoId, ct);
        if (existente is not null)
        {
            // idempotência: consumer SQS pode reentregar a mesma mensagem
            _logger.LogInformation("Orçamento já existe para a OS {OrdemServicoId}, ignorando duplicata.", request.OrdemServicoId);
            return Result<OrcamentoResponse>.Ok(existente.ParaResponse());
        }

        var itens = new List<ItemOrcamento>();

        foreach (var peca in request.Pecas)
        {
            var preco = await _cadastrosServiceClient.ObterPrecoPecaAsync(peca.Id, ct);
            if (preco is null)
                return Result<OrcamentoResponse>.Falha($"Peça {peca.Id} não encontrada no Cadastros Service.");

            itens.Add(new ItemOrcamento(preco.Descricao, peca.Quantidade, new Dinheiro(preco.PrecoUnitario)));
        }

        foreach (var servico in request.Servicos)
        {
            var preco = await _cadastrosServiceClient.ObterPrecoServicoAsync(servico.Id, ct);
            if (preco is null)
                return Result<OrcamentoResponse>.Falha($"Serviço {servico.Id} não encontrado no Cadastros Service.");

            itens.Add(new ItemOrcamento(preco.Descricao, servico.Quantidade, new Dinheiro(preco.PrecoUnitario)));
        }

        var orcamento = Domain.Entities.Orcamento.Gerar(request.OrdemServicoId, itens);

        await _orcamentoRepository.AdicionarAsync(orcamento, ct);
        await _orcamentoRepository.SalvarAlteracoesAsync(ct);

        foreach (var domainEvent in orcamento.DomainEvents)
            await _eventPublisher.PublicarAsync(domainEvent, ct);

        return Result<OrcamentoResponse>.Ok(orcamento.ParaResponse());
    }
}
