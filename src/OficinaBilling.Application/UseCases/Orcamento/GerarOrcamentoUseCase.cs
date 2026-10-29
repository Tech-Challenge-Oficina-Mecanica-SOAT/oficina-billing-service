using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.Mappers;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Application.UseCases.Orcamento;

// Disparado pelo consumer SQS que escuta "diagnostico.concluido" (Execução Service).
// Fluxo: busca preço de cada peça/serviço no Cadastros -> soma -> persiste -> enfileira "orcamento.gerado".
public class GerarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly ICadastrosServiceClient _cadastrosServiceClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppLogger<GerarOrcamentoUseCase> _logger;

    public GerarOrcamentoUseCase(
        IOrcamentoRepository orcamentoRepository,
        ICadastrosServiceClient cadastrosServiceClient,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        IAppLogger<GerarOrcamentoUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _cadastrosServiceClient = cadastrosServiceClient;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OrcamentoResponse>> ExecutarAsync(GerarOrcamentoRequest request, CancellationToken ct = default)
    {
        // Idempotência de negócio: o consumer SQS pode reentregar a mesma mensagem.
        var existente = await _orcamentoRepository.ObterPorOsIdAsync(request.OsId, ct);
        if (existente is not null)
        {
            _logger.LogInformation("Orçamento já existe para a OS {OsId}, ignorando duplicata.", request.OsId);
            return Result<OrcamentoResponse>.Ok(existente.ParaResponse());
        }

        var itens = new List<Domain.Entities.OrcamentoItem>();

        foreach (var peca in request.Pecas)
        {
            var preco = await _cadastrosServiceClient.ObterPrecoPecaAsync(peca.Id, ct);
            if (preco is null)
                return Result<OrcamentoResponse>.Falha($"Peça {peca.Id} não encontrada no Cadastros Service.");

            itens.Add(Domain.Entities.OrcamentoItem.Criar(TipoItem.Peca, preco.Id, preco.Nome, peca.Quantidade, preco.PrecoUnitario));
        }

        foreach (var servico in request.Servicos)
        {
            var preco = await _cadastrosServiceClient.ObterPrecoServicoAsync(servico.Id, ct);
            if (preco is null)
                return Result<OrcamentoResponse>.Falha($"Serviço {servico.Id} não encontrado no Cadastros Service.");

            itens.Add(Domain.Entities.OrcamentoItem.Criar(TipoItem.Servico, preco.Id, preco.Nome, servico.Quantidade, preco.PrecoUnitario));
        }

        var orcamento = Domain.Entities.Orcamento.Gerar(request.OsId, itens);

        await _orcamentoRepository.AdicionarAsync(orcamento, ct);

        foreach (var domainEvent in orcamento.DomainEvents)
            await _eventPublisher.EnfileirarAsync(domainEvent, ct);

        // Outbox: grava orçamento + evento pendente na MESMA transação.
        await _unitOfWork.SalvarAlteracoesAsync(ct);

        return Result<OrcamentoResponse>.Ok(orcamento.ParaResponse());
    }
}
