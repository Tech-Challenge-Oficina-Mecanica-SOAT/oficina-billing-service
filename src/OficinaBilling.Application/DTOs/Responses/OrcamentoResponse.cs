namespace OficinaBilling.Application.DTOs.Responses;

public sealed record OrcamentoResponse(
    Guid Id,
    Guid OrdemServicoId,
    string Status,
    decimal ValorTotal,
    DateTime CriadoEm,
    DateTime? AprovadoEm,
    IReadOnlyList<ItemOrcamentoResponse> Itens);

public sealed record ItemOrcamentoResponse(string Descricao, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
