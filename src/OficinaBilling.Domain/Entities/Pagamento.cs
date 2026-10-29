using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Events;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Domain.Entities;

public class Pagamento : IAggregateRoot
{
    private readonly List<object> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid OrcamentoId { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public Dinheiro Valor { get; private set; } = Dinheiro.Zero;
    public StatusPagamento Status { get; private set; }

    /// <summary>Id da preferência (Checkout Pro) criada no Mercado Pago.</summary>
    public string? MercadoPagoPreferenceId { get; private set; }
    public string? LinkPagamento { get; private set; }

    /// <summary>Id do pagamento no MP. Só é conhecido depois da primeira notificação (reconciliação).</summary>
    public string? MercadoPagoPaymentId { get; private set; }

    public DateTime CriadoEm { get; private set; }
    public DateTime? ConfirmadoEm { get; private set; }
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

    private Pagamento() { } // EF Core

    /// <summary>
    /// O Id é gerado antes da chamada ao MP porque vira o <c>external_reference</c> da preferência:
    /// é por ele que o webhook encontra este pagamento.
    /// </summary>
    public static Pagamento Criar(Guid orcamentoId, Guid ordemServicoId, Dinheiro valor)
    {
        if (valor.Valor <= 0)
            throw new ArgumentException("O valor da cobrança deve ser maior que zero.", nameof(valor));

        return new Pagamento
        {
            Id = Guid.NewGuid(),
            OrcamentoId = orcamentoId,
            OrdemServicoId = ordemServicoId,
            Valor = valor,
            Status = StatusPagamento.Pendente,
            CriadoEm = DateTime.UtcNow
        };
    }

    public void RegistrarCobranca(string preferenceId, string linkPagamento)
    {
        MercadoPagoPreferenceId = preferenceId;
        LinkPagamento = linkPagamento;
    }

    public void RegistrarPagamentoMercadoPago(string mercadoPagoPaymentId) =>
        MercadoPagoPaymentId = mercadoPagoPaymentId;

    /// <summary>Idempotente: devolve false (sem evento) se o pagamento já saiu de Pendente.</summary>
    public bool Confirmar()
    {
        if (Status != StatusPagamento.Pendente)
            return false;

        Status = StatusPagamento.Confirmado;
        ConfirmadoEm = DateTime.UtcNow;
        _domainEvents.Add(new PagamentoConfirmadoEvent(Id, OrdemServicoId));
        return true;
    }

    public bool Recusar()
    {
        if (Status != StatusPagamento.Pendente)
            return false;

        Status = StatusPagamento.Recusado;
        _domainEvents.Add(new PagamentoRecusadoEvent(Id, OrdemServicoId));
        return true;
    }

    /// <summary>Compensação: cobrança ainda em aberto cancelada porque a OS foi cancelada.</summary>
    public bool Cancelar()
    {
        if (Status != StatusPagamento.Pendente)
            return false;

        Status = StatusPagamento.Cancelado;
        return true;
    }

    /// <summary>Compensação: pagamento já confirmado que precisa ser devolvido ao cliente.</summary>
    public bool Estornar()
    {
        if (Status != StatusPagamento.Confirmado)
            return false;

        Status = StatusPagamento.Estornado;
        return true;
    }

    public void LimparEventos() => _domainEvents.Clear();
}
