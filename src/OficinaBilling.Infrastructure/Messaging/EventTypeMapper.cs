using OficinaBilling.Domain.Events;

namespace OficinaBilling.Infrastructure.Messaging;

// Único lugar que sabe traduzir Domain Event -> nome de topic (contrato de eventos).
public static class EventTypeMapper
{
    public static string EventTypeDe<TEvento>(TEvento evento) => evento switch
    {
        OrcamentoGeradoEvent => "orcamento.gerado",
        OrcamentoAprovadoEvent => "orcamento.aprovado",
        OrcamentoRecusadoEvent => "orcamento.recusado",
        PagamentoConfirmadoEvent => "pagamento.confirmado",
        PagamentoRecusadoEvent => "pagamento.recusado",
        _ => throw new InvalidOperationException($"Evento {typeof(TEvento).Name} não mapeado para nenhum topic.")
    };
}
