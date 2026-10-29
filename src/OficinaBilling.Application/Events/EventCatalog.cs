using OficinaBilling.Domain.Events;

namespace OficinaBilling.Application.Events;

/// <summary>Mapeia cada Domain Event para o nome do tópico SNS (contrato com os outros serviços).</summary>
public static class EventCatalog
{
    public static string TopicoDe(object evento) => evento switch
    {
        OrcamentoGeradoEvent => "orcamento.gerado",
        OrcamentoAprovadoEvent => "orcamento.aprovado",
        OrcamentoRecusadoEvent => "orcamento.recusado",
        PagamentoConfirmadoEvent => "pagamento.confirmado",
        PagamentoRecusadoEvent => "pagamento.recusado",
        _ => throw new InvalidOperationException($"Evento {evento.GetType().Name} não mapeado para nenhum tópico.")
    };
}
