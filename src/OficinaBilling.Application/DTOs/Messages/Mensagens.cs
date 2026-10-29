namespace OficinaBilling.Application.DTOs.Messages;

// Payloads consumidos via SQS. Ver docs/events.md para o contrato combinado com os outros serviços.
public sealed record ExecucaoFinalizadaMessage(Guid OrdemServicoId);

public sealed record OsCanceladaMessage(Guid OrdemServicoId);
