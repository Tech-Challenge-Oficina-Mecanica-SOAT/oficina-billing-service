using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Events;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Domain.Entities;

public class Pagamento
{
    private readonly List<object> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid OrcamentoId { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public Dinheiro Valor { get; private set; } = Dinheiro.Zero;
    public StatusPagamento Status { get; private set; }
    public string? MercadoPagoPaymentId { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime? ConfirmadoEm { get; private set; }
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

    private Pagamento() { } // EF Core

    public static Pagamento CriarCobranca(Guid orcamentoId, Guid ordemServicoId, Dinheiro valor, string mercadoPagoPaymentId)
    {
        return new Pagamento
        {
            Id = Guid.NewGuid(),
            OrcamentoId = orcamentoId,
            OrdemServicoId = ordemServicoId,
            Valor = valor,
            Status = StatusPagamento.Pendente,
            MercadoPagoPaymentId = mercadoPagoPaymentId,
            CriadoEm = DateTime.UtcNow
        };
    }

    // Chamado a partir do webhook do Mercado Pago (ver ProcessarWebhookMercadoPago).
    public void Confirmar()
    {
        if (Status != StatusPagamento.Pendente)
            return; // idempotência: webhook duplicado não deve lançar exceção

        Status = StatusPagamento.Confirmado;
        ConfirmadoEm = DateTime.UtcNow;
        _domainEvents.Add(new PagamentoConfirmadoEvent(Id, OrdemServicoId));
    }

    public void Recusar()
    {
        if (Status != StatusPagamento.Pendente)
            return;

        Status = StatusPagamento.Recusado;
        _domainEvents.Add(new PagamentoRecusadoEvent(Id, OrdemServicoId));
    }
}
