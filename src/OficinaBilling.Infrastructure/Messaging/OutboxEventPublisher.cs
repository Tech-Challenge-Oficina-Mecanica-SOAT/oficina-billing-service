using System.Diagnostics;
using System.Text.Json;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Messaging;

// Implementa IEventPublisher gravando na tabela Outbox — NÃO chama o SNS
// diretamente. Quem publica de fato no SNS é o OutboxPublisherBackgroundService,
// rodando em background e lendo a mesma tabela. Isso é o Outbox Pattern: a
// escrita do evento cai na mesma transação do agregado (ver UnitOfWork).
public class OutboxEventPublisher : IEventPublisher
{
    private const string ProducerName = "billing-service";
    private const string EventVersion = "1.0";

    // O contrato (envelope + data) é camelCase: eventType, eventId, osId, orcamentoId...
    // Sem isso o System.Text.Json gera PascalCase e nem o consumer deste serviço consegue
    // ler o próprio evento (GetProperty("eventType") diferencia maiúsculas de minúsculas).
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOutboxRepository _outboxRepository;

    public OutboxEventPublisher(IOutboxRepository outboxRepository) => _outboxRepository = outboxRepository;

    public async Task EnfileirarAsync<TEvento>(TEvento evento, CancellationToken ct = default) where TEvento : notnull
    {
        var eventId = Guid.NewGuid();
        var eventType = EventTypeMapper.EventTypeDe(evento);

        var envelope = new EventEnvelope(
            EventId: eventId,
            EventType: eventType,
            EventVersion: EventVersion,
            OccurredAt: DateTime.UtcNow,
            CorrelationId: Activity.Current?.Id ?? Guid.NewGuid().ToString(),
            Producer: ProducerName,
            Data: evento);

        var payload = JsonSerializer.Serialize(envelope, JsonOptions);
        var mensagem = OutboxMessage.Criar(eventId, eventType, payload);

        await _outboxRepository.AdicionarAsync(mensagem, ct);
        // Sem SaveChanges aqui de propósito: quem confirma a transação é o
        // IUnitOfWork.SalvarAlteracoesAsync() chamado pelo use case.
    }
}
