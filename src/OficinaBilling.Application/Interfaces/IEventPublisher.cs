namespace OficinaBilling.Application.Interfaces;

/// <summary>Porta de saída para o broker. Usada pelo dispatcher do outbox, não pelos use cases.</summary>
public interface IEventPublisher
{
    Task PublicarAsync(string topico, string mensagemJson, IReadOnlyDictionary<string, string> atributos, CancellationToken ct = default);
}
