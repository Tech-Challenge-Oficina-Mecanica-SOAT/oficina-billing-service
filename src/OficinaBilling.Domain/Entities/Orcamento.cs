using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Events;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Domain.Entities;

// Agregado raiz. Não referencia entidades de outros serviços por FK —
// guarda apenas os IDs (OrdemServicoId) recebidos via evento/REST.
public class Orcamento : IAggregateRoot
{
    private readonly List<ItemOrcamento> _itens = new();
    private readonly List<object> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public StatusOrcamento Status { get; private set; }
    public Dinheiro ValorTotal { get; private set; } = Dinheiro.Zero;
    public DateTime CriadoEm { get; private set; }
    public DateTime? AprovadoEm { get; private set; }
    public DateTime? CanceladoEm { get; private set; }
    public IReadOnlyCollection<ItemOrcamento> Itens => _itens.AsReadOnly();
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

    private Orcamento() { } // EF Core

    public static Orcamento Gerar(Guid ordemServicoId, IEnumerable<ItemOrcamento> itens)
    {
        var lista = itens.ToList();
        if (lista.Count == 0)
            throw new ArgumentException("Um orçamento precisa de ao menos um item.", nameof(itens));

        var orcamento = new Orcamento
        {
            Id = Guid.NewGuid(),
            OrdemServicoId = ordemServicoId,
            Status = StatusOrcamento.Gerado,
            CriadoEm = DateTime.UtcNow
        };

        orcamento._itens.AddRange(lista);
        orcamento.ValorTotal = orcamento._itens.Aggregate(Dinheiro.Zero, (total, i) => total + i.Subtotal);
        orcamento._domainEvents.Add(new OrcamentoGeradoEvent(orcamento.Id, orcamento.OrdemServicoId, orcamento.ValorTotal.Valor));

        return orcamento;
    }

    public void Aprovar()
    {
        if (Status != StatusOrcamento.Gerado)
            throw new InvalidOperationException("Só é possível aprovar um orçamento com status 'Gerado'.");

        Status = StatusOrcamento.Aprovado;
        AprovadoEm = DateTime.UtcNow;
        _domainEvents.Add(new OrcamentoAprovadoEvent(Id, OrdemServicoId));
    }

    public void Recusar()
    {
        if (Status != StatusOrcamento.Gerado)
            throw new InvalidOperationException("Só é possível recusar um orçamento com status 'Gerado'.");

        Status = StatusOrcamento.Recusado;
        _domainEvents.Add(new OrcamentoRecusadoEvent(Id, OrdemServicoId));
    }

    /// <summary>
    /// Compensação do Saga (OS cancelada). Idempotente: devolve false se já estava encerrado.
    /// Não publica evento — quem cancelou a OS é o orquestrador, que já conhece o desfecho.
    /// </summary>
    public bool Cancelar()
    {
        if (Status is StatusOrcamento.Cancelado or StatusOrcamento.Recusado)
            return false;

        Status = StatusOrcamento.Cancelado;
        CanceladoEm = DateTime.UtcNow;
        return true;
    }

    public void LimparEventos() => _domainEvents.Clear();
}

public sealed record ItemOrcamento(string Descricao, int Quantidade, Dinheiro PrecoUnitario)
{
    public Dinheiro Subtotal => new(PrecoUnitario.Valor * Quantidade);
}
