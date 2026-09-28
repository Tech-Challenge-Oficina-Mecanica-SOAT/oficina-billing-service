namespace OficinaBilling.Application.DTOs.Responses;

public sealed record OrcamentoResponse(
    Guid Id,
    Guid OrdemServicoId,
    string Status,
    decimal ValorTotal,
    DateTime CriadoEm);
