namespace OficinaBilling.Domain.Interfaces;

public interface IIdempotenciaRepository
{
    Task<bool> JaProcessadoAsync(string chave, CancellationToken ct = default);
    Task MarcarProcessadoAsync(string chave, CancellationToken ct = default);
}
