namespace OficinaBilling.Application.Interfaces;

public interface IIdempotencyStore
{
    Task<bool> JaProcessadoAsync(string chave, CancellationToken ct = default);
    Task RegistrarAsync(string chave, CancellationToken ct = default);
}
