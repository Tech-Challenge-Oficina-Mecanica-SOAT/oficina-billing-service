namespace OficinaBilling.Application.DTOs.Requests;

// Corpo de POST /orcamentos/{orcamentoId}/recusar.
public sealed record RecusarOrcamentoRequest(string? Motivo);
