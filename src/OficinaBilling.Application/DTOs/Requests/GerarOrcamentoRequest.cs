namespace OficinaBilling.Application.DTOs.Requests;

// Recebido do evento SNS "diagnostico.concluido" (publicado pelo Execução Service).
public sealed record GerarOrcamentoRequest(
    Guid OrdemServicoId,
    List<ItemDiagnostico> Pecas,
    List<ItemDiagnostico> Servicos);

public sealed record ItemDiagnostico(Guid Id, int Quantidade);
