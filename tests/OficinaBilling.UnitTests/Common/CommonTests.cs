using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using OficinaBilling.Application.Common;
using OficinaBilling.Application.Events;
using OficinaBilling.Domain.Events;

namespace OficinaBilling.UnitTests.Common;

public class MercadoPagoWebhookSignatureTests
{
    private const string Segredo = "segredo-do-webhook";

    private static string Assinar(string dataId, string requestId, string ts)
    {
        var manifesto = $"id:{dataId};request-id:{requestId};ts:{ts};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Segredo));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifesto))).ToLowerInvariant();
    }

    [Fact]
    public void Aceita_assinatura_valida_com_id_em_maiusculas()
    {
        var v1 = Assinar("abc123", "req-1", "1700000000");

        MercadoPagoWebhookSignature.Validar(Segredo, $"ts=1700000000,v1={v1}", "req-1", "ABC123").Should().BeTrue();
    }

    [Theory]
    [InlineData("req-OUTRO", "abc123")]
    [InlineData("req-1", "outro-id")]
    public void Rejeita_manifesto_adulterado(string requestId, string dataId)
    {
        var v1 = Assinar("abc123", "req-1", "1700000000");

        MercadoPagoWebhookSignature.Validar(Segredo, $"ts=1700000000,v1={v1}", requestId, dataId).Should().BeFalse();
    }

    [Fact]
    public void Rejeita_segredo_errado()
    {
        var v1 = Assinar("1", "r", "10");

        MercadoPagoWebhookSignature.Validar("outro-segredo", $"ts=10,v1={v1}", "r", "1").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lixo")]
    [InlineData("ts=1")]
    [InlineData("v1=abc")]
    public void Rejeita_header_ausente_ou_malformado(string? header) =>
        MercadoPagoWebhookSignature.Validar(Segredo, header, "r", "1").Should().BeFalse();

    [Fact]
    public void Rejeita_segredo_vazio() =>
        MercadoPagoWebhookSignature.Validar("", "ts=1,v1=aa", "r", "1").Should().BeFalse();
}

public class EventoEnvelopeTests
{
    [Theory]
    [InlineData(typeof(OrcamentoGeradoEvent), "orcamento.gerado")]
    [InlineData(typeof(OrcamentoAprovadoEvent), "orcamento.aprovado")]
    [InlineData(typeof(OrcamentoRecusadoEvent), "orcamento.recusado")]
    [InlineData(typeof(PagamentoConfirmadoEvent), "pagamento.confirmado")]
    [InlineData(typeof(PagamentoRecusadoEvent), "pagamento.recusado")]
    public void Cada_evento_tem_seu_topico(Type tipo, string topico)
    {
        var evento = tipo == typeof(OrcamentoGeradoEvent)
            ? new OrcamentoGeradoEvent(Guid.NewGuid(), Guid.NewGuid(), 10m)
            : Activator.CreateInstance(tipo, Guid.NewGuid(), Guid.NewGuid())!;

        EventCatalog.TopicoDe(evento).Should().Be(topico);
    }

    [Fact]
    public void Evento_desconhecido_lanca() =>
        FluentActions.Invoking(() => EventCatalog.TopicoDe(new object())).Should().Throw<InvalidOperationException>();

    [Fact]
    public void Envelope_traz_metadados_e_dados_em_camel_case()
    {
        var os = Guid.NewGuid();

        var e = EventoEnvelope.Criar(new PagamentoConfirmadoEvent(Guid.NewGuid(), os), "corr-1");

        e.EventType.Should().Be("pagamento.confirmado");
        e.CorrelationId.Should().Be("corr-1");
        e.EventVersion.Should().Be("1.0");
        e.EventId.Should().NotBeEmpty();
        e.Producer.Should().Be("billing-service");
        e.Data.GetProperty("osId").GetGuid().Should().Be(os);
    }

    [Fact]
    public void Envelope_serializa_em_camel_case()
    {
        var json = JsonSerializer.Serialize(EventoEnvelope.Criar(new OrcamentoRecusadoEvent(Guid.NewGuid(), Guid.NewGuid()), null), JsonDefaults.Options);

        json.Should().Contain("\"eventType\":\"orcamento.recusado\"")
            .And.Contain("\"eventId\":")
            .And.Contain("\"eventVersion\":\"1.0\"")
            .And.Contain("\"occurredAt\":")
            .And.Contain("\"producer\":\"billing-service\"")
            .And.Contain("\"data\":{")
            .And.Contain("\"osId\":");
    }
}

public class CorrelationContextTests
{
    [Fact]
    public void Define_e_restaura_o_valor_anterior()
    {
        CorrelationContext.Atual.Should().BeNull();

        using (CorrelationContext.Definir("a"))
        {
            CorrelationContext.Atual.Should().Be("a");
            using (CorrelationContext.Definir("b"))
                CorrelationContext.Atual.Should().Be("b");
            CorrelationContext.Atual.Should().Be("a");
        }

        CorrelationContext.Atual.Should().BeNull();
    }
}
