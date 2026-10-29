namespace OficinaBilling.Domain.Entities;

public interface IAggregateRoot
{
    IReadOnlyCollection<object> DomainEvents { get; }
    void LimparEventos();
}
