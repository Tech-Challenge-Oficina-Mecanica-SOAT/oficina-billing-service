using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Domain.Interfaces;

public interface IOutboxRepository
{
    Task AdicionarAsync(OutboxMessage mensagem, CancellationToken ct = default);
    Task<List<OutboxMessage>> ObterPendentesAsync(int limite = 20, CancellationToken ct = default);
    Task MarcarPublicadaAsync(Guid id, CancellationToken ct = default);
    Task RegistrarFalhaAsync(Guid id, string erro, CancellationToken ct = default);
}
