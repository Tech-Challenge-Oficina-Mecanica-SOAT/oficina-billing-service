namespace OficinaBilling.Application.DTOs.Responses;

public sealed record PagamentoResponse(
    Guid Id,
    Guid OrcamentoId,
    Guid OrdemServicoId,
    string Status,
    decimal Valor,
    string? LinkPagamento,
    string? MercadoPagoPaymentId,
    DateTime CriadoEm,
    DateTime? ConfirmadoEm);
