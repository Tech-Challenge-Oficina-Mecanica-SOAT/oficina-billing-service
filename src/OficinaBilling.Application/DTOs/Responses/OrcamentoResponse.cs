namespace OficinaBilling.Application.DTOs.Responses;

public sealed record OrcamentoResponse(
    Guid Id,
    Guid OsId,
    string Status,
    IReadOnlyList<OrcamentoPecaResponse> Pecas,
    IReadOnlyList<OrcamentoServicoResponse> Servicos,
    IReadOnlyList<ItemOrcamentoLegadoResponse> ItensLegados,
    decimal ValorTotal,
    DateTime ValidadeAte,
    DateTime GeradoEm);

public sealed record OrcamentoPecaResponse(Guid PecaId, int Quantidade, decimal PrecoUnitario, decimal Subtotal);

public sealed record OrcamentoServicoResponse(Guid ServicoId, int Quantidade, decimal PrecoUnitario, decimal Subtotal);

public sealed record ItemOrcamentoLegadoResponse(string Descricao, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
