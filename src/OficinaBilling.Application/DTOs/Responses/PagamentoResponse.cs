namespace OficinaBilling.Application.DTOs.Responses;

public sealed record PagamentoResponse(
    Guid Id,
    Guid OsId,
    Guid OrcamentoId,
    decimal Valor,
    string Status,
    string? MercadoPagoId,
    string? LinkPagamento,
    DateTime CriadoEm);
