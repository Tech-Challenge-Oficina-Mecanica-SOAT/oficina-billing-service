namespace OficinaBilling.Infrastructure.Persistence.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Topico { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public DateTime OcorridoEm { get; set; }
    public DateTime? ProcessadoEm { get; set; }
    public int Tentativas { get; set; }
    public string? UltimoErro { get; set; }
}

public class IdempotencyKeyEntry
{
    public string Chave { get; set; } = string.Empty;
    public DateTime ProcessadoEm { get; set; }
}
