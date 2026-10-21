namespace OficinaBilling.Domain.Entities;

// Outbox Pattern (pedido explícito no plano): o evento é gravado na MESMA
// transação que a mudança de estado do agregado (Orcamento/Pagamento) e só
// depois publicado no SNS por um worker separado. Evita o cenário em que o
// banco salva mas o SNS falha (ou vice-versa) e o sistema fica inconsistente.
public class OutboxMessage
{
    public Guid Id { get; private set; }              // = eventId do envelope
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty; // envelope completo, já serializado
    public DateTime CriadoEm { get; private set; }
    public DateTime? PublicadoEm { get; private set; }
    public int Tentativas { get; private set; }
    public string? UltimoErro { get; private set; }

    private OutboxMessage() { } // EF Core

    public static OutboxMessage Criar(Guid eventId, string eventType, string payload) => new()
    {
        Id = eventId,
        EventType = eventType,
        Payload = payload,
        CriadoEm = DateTime.UtcNow
    };

    public void MarcarPublicada() => PublicadoEm = DateTime.UtcNow;

    public void RegistrarFalha(string erro)
    {
        Tentativas++;
        UltimoErro = erro;
    }
}
