namespace OficinaBilling.Application.DTOs.Responses;

// Espelha o schema "Orcamento" do contrato openapi/billing.yaml.
public sealed record OrcamentoResponse(
    Guid Id,
    Guid OsId,
    string Status,
    List<ItemResponse> Pecas,
    List<ItemResponse> Servicos,
    decimal ValorTotal,
    DateTime ValidadeAte,
    DateTime GeradoEm);

public sealed record ItemResponse(Guid ReferenciaId, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
