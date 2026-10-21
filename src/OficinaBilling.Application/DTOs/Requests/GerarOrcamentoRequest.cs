namespace OficinaBilling.Application.DTOs.Requests;

// Montado a partir do "data" do evento "diagnostico.concluido" (ver contratos/eventos/schemas.md).
public sealed record GerarOrcamentoRequest(
    Guid OsId,
    Guid DiagnosticoId,
    List<ItemDiagnostico> Pecas,
    List<ItemDiagnostico> Servicos);

public sealed record ItemDiagnostico(Guid Id, int Quantidade);
