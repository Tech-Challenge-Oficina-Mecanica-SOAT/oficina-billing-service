namespace OficinaBilling.Domain.Entities;

// Idempotência do lado consumidor (Inbox): guarda o eventId dos eventos SQS e a chave
// "mp:{paymentId}:{status}" dos webhooks do MP: tudo que já foi processado.
// Retenção sugerida no contrato: 7 dias.
public class EventoProcessado
{
    public string Chave { get; private set; } = string.Empty;
    public DateTime ProcessadoEm { get; private set; }

    private EventoProcessado() { } // EF Core

    public static EventoProcessado Criar(string chave) => new()
    {
        Chave = chave,
        ProcessadoEm = DateTime.UtcNow
    };
}
