namespace OficinaBilling.Application.DTOs.Responses;

// Espelha o schema "Pagamento" do contrato openapi/billing.yaml.
public sealed record PagamentoResponse(
    Guid Id,
    Guid OsId,
    Guid OrcamentoId,
    decimal Valor,
    string Status,
    string? MercadoPagoId,
    string? LinkPagamento,
    DateTime CriadoEm);
