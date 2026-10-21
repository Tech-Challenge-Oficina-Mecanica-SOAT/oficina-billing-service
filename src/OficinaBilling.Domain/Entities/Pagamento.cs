using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Events;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Domain.Entities;

public class Pagamento
{
    private readonly List<object> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid OsId { get; private set; }
    public Guid OrcamentoId { get; private set; }
    public Dinheiro Valor { get; private set; } = Dinheiro.Zero;
    public StatusPagamento Status { get; private set; }
    public string? PreferenceId { get; private set; }     // id da preferência criada no Mercado Pago
    public string? LinkPagamento { get; private set; }
    public string? MercadoPagoId { get; private set; }    // id do "payment", chega no webhook — usado p/ reconciliação
    public string? MotivoRecusa { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime? ResolvidoEm { get; private set; }

    public IReadOnlyCollection<object> DomainEvents => _domainEvents;

    private Pagamento() { } // EF Core

    public static Pagamento CriarCobranca(Guid osId, Guid orcamentoId, Dinheiro valor, string preferenceId, string linkPagamento) => new()
    {
        Id = Guid.NewGuid(),
        OsId = osId,
        OrcamentoId = orcamentoId,
        Valor = valor,
        Status = StatusPagamento.Pendente,
        PreferenceId = preferenceId,
        LinkPagamento = linkPagamento,
        CriadoEm = DateTime.UtcNow
    };

    // Chamado a partir do webhook do Mercado Pago, DEPOIS de confirmado o status
    // real via consulta à API do MP (nunca confiar cegamente no payload do webhook).
    // Idempotente: retorna false (sem lançar exceção) se já estava Confirmado —
    // essencial porque o MP reenvia a mesma notificação várias vezes.
    //
    // Aceita também Recusado -> Confirmado: no Checkout Pro o cliente pode tentar de novo
    // (outro cartão) na MESMA preferência depois de uma recusa. Se o dinheiro entrou,
    // ignorar a aprovação deixaria o cliente cobrado com o sistema dizendo "recusado".
    public bool Confirmar(string mercadoPagoId)
    {
        if (Status == StatusPagamento.Confirmado)
            return false;

        Status = StatusPagamento.Confirmado;
        MercadoPagoId = mercadoPagoId;
        MotivoRecusa = null;
        ResolvidoEm = DateTime.UtcNow;
        _domainEvents.Add(new PagamentoConfirmadoEvent(OsId, Id, Valor.Valor, ResolvidoEm.Value));
        return true;
    }

    public bool Recusar(string mercadoPagoId, string motivo)
    {
        if (Status != StatusPagamento.Pendente)
            return false;

        Status = StatusPagamento.Recusado;
        MercadoPagoId = mercadoPagoId;
        MotivoRecusa = motivo;
        ResolvidoEm = DateTime.UtcNow;
        _domainEvents.Add(new PagamentoRecusadoEvent(OsId, Id, motivo, ResolvidoEm.Value));
        return true;
    }
}
