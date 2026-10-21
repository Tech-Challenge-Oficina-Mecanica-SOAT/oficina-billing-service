using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Events;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Domain.Entities;

public class Orcamento
{
    public static readonly TimeSpan ValidadePadrao = TimeSpan.FromDays(7);

    private readonly List<OrcamentoItem> _itens = new();
    private readonly List<object> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid OsId { get; private set; }
    public StatusOrcamento Status { get; private set; }
    public Dinheiro ValorTotal { get; private set; } = Dinheiro.Zero;
    public DateTime GeradoEm { get; private set; }
    public DateTime ValidadeAte { get; private set; }
    public DateTime? DecididoEm { get; private set; }
    public string? MotivoRecusa { get; private set; }

    public IReadOnlyCollection<OrcamentoItem> Itens => _itens;
    public IReadOnlyCollection<object> DomainEvents => _domainEvents;

    private Orcamento() { } // EF Core

    public static Orcamento Gerar(Guid osId, IEnumerable<OrcamentoItem> itens, DateTime? agora = null)
    {
        var lista = itens.ToList();
        if (lista.Count == 0)
            throw new ArgumentException("Orçamento precisa ter ao menos um item.", nameof(itens));

        var now = agora ?? DateTime.UtcNow;
        var orcamento = new Orcamento
        {
            Id = Guid.NewGuid(),
            OsId = osId,
            Status = StatusOrcamento.Gerado,
            GeradoEm = now,
            ValidadeAte = now + ValidadePadrao
        };
        orcamento._itens.AddRange(lista);
        orcamento.ValorTotal = lista.Aggregate(Dinheiro.Zero, (total, i) => total + i.Subtotal);

        orcamento._domainEvents.Add(new OrcamentoGeradoEvent(
            orcamento.OsId, orcamento.Id, orcamento.ValorTotal.Valor, orcamento.ValidadeAte, orcamento.GeradoEm));

        return orcamento;
    }

    public bool EstaExpirado(DateTime? agora = null) =>
        Status == StatusOrcamento.Gerado && (agora ?? DateTime.UtcNow) > ValidadeAte;

    public void Aprovar(DateTime? agora = null)
    {
        var now = agora ?? DateTime.UtcNow;

        if (EstaExpirado(now))
        {
            Status = StatusOrcamento.Expirado;
            throw new InvalidOperationException("Orçamento expirado; não pode mais ser aprovado.");
        }
        if (Status != StatusOrcamento.Gerado)
            throw new InvalidOperationException($"Só é possível aprovar um orçamento 'Gerado' (status atual: {Status}).");

        Status = StatusOrcamento.Aprovado;
        DecididoEm = now;
        _domainEvents.Add(new OrcamentoAprovadoEvent(OsId, Id, now));
    }

    public void Recusar(string? motivo, DateTime? agora = null)
    {
        if (Status != StatusOrcamento.Gerado)
            throw new InvalidOperationException($"Só é possível recusar um orçamento 'Gerado' (status atual: {Status}).");

        var now = agora ?? DateTime.UtcNow;
        Status = StatusOrcamento.Recusado;
        MotivoRecusa = motivo;
        DecididoEm = now;
        _domainEvents.Add(new OrcamentoRecusadoEvent(OsId, Id, motivo, now));
    }

    /// <summary>
    /// Compensação da saga: chamado quando chega o evento "os.cancelada".
    /// Só cancela um orçamento ainda pendente de decisão; não desfaz um já aprovado/recusado.
    /// </summary>
    public bool CancelarPorCompensacao(DateTime? agora = null)
    {
        if (Status != StatusOrcamento.Gerado)
            return false;

        Status = StatusOrcamento.Cancelado;
        DecididoEm = agora ?? DateTime.UtcNow;
        return true;
    }
}
