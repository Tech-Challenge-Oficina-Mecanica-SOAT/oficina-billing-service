using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Application.Interfaces;

/// <summary>
/// Outbox Pattern: o evento é gravado na MESMA transação do agregado (mesmo SaveChanges).
/// Um dispatcher em background publica no SNS depois — nunca se perde evento se o SNS estiver fora.
/// </summary>
public interface IOutbox
{
    void Enfileirar(object evento);
}

public static class OutboxExtensions
{
    public static void EnfileirarEventos(this IOutbox outbox, IAggregateRoot agregado)
    {
        foreach (var evento in agregado.DomainEvents)
            outbox.Enfileirar(evento);
        agregado.LimparEventos();
    }
}
