using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OficinaBilling.Application.Common;

namespace OficinaBilling.Application.Messaging;

/// <summary>
/// Mensagem já "desembrulhada" do SQS. Aceita três formatos:
///  1. Envelope do SNS (<c>Type=Notification</c>, <c>TopicArn</c>, <c>Message</c> em string) — entrega padrão SNS→SQS;
///  2. JSON com envelope de domínio (<c>tipo</c> + <c>dados</c>) — o formato que o Billing publica;
///  3. JSON puro com o campo <c>tipo</c>.
/// O nome do tópico lógico (ex.: "diagnostico.concluido") vem do sufixo do TopicArn.
/// </summary>
public sealed record MensagemEntrante(string Topico, string PayloadJson, string? CorrelationId)
{
    private static readonly Regex PrefixoTopico = new("^oficina-[^-]+-", RegexOptions.Compiled);

    public static MensagemEntrante Parse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var raiz = doc.RootElement;

            string? topico = null;
            string? correlation = null;
            string interno = body;

            if (raiz.ValueKind == JsonValueKind.Object
                && string.Equals(Texto(raiz, "Type"), "Notification", StringComparison.OrdinalIgnoreCase)
                && Texto(raiz, "Message") is { } mensagem)
            {
                interno = mensagem;
                var arn = Texto(raiz, "TopicArn");
                if (arn is not null)
                    topico = TopicoDoArn(arn);

                if (raiz.TryGetProperty("MessageAttributes", out var attrs)
                    && Propriedade(attrs, "correlationId") is { } attr)
                    correlation = Texto(attr, "Value");
            }

            using var docInterno = JsonDocument.Parse(interno);
            var corpo = docInterno.RootElement;
            string payload = interno;

            if (corpo.ValueKind == JsonValueKind.Object)
            {
                topico ??= Texto(corpo, "eventType") ?? Texto(corpo, "tipo") ?? Texto(corpo, "type");
                correlation ??= Texto(corpo, "correlationId");

                if (Propriedade(corpo, "data") is { ValueKind: JsonValueKind.Object } data)
                    payload = data.GetRawText();
                else if (Propriedade(corpo, "dados") is { ValueKind: JsonValueKind.Object } dados)
                    payload = dados.GetRawText();
            }

            if (string.IsNullOrWhiteSpace(topico))
                throw new FormatException("Não foi possível determinar o tipo (tópico) da mensagem.");

            return new MensagemEntrante(topico, NormalizarPayload(topico, payload), correlation);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Corpo da mensagem não é um JSON válido.", ex);
        }
    }

    public static string TopicoDoArn(string topicArn)
    {
        var nome = topicArn[(topicArn.LastIndexOf(':') + 1)..];
        return PrefixoTopico.Replace(nome, string.Empty).Replace('-', '.');
    }

    private static string NormalizarPayload(string topico, string payload)
    {
        var json = JsonNode.Parse(payload);
        if (json is not JsonObject objeto)
            return payload;

        if (topico is "diagnostico.concluido" or "execucao.finalizada" or "os.cancelada")
            CopiarAlias(objeto, "ordemServicoId", "osId");

        if (topico == "diagnostico.concluido")
        {
            NormalizarItens(objeto, "pecas", "pecaId");
            NormalizarItens(objeto, "servicos", "servicoId");
        }

        return objeto.ToJsonString(JsonDefaults.Options);
    }

    private static void NormalizarItens(JsonObject objeto, string colecao, string aliasId)
    {
        if (Propriedade(objeto, colecao) is not JsonArray itens)
            return;

        foreach (var item in itens.OfType<JsonObject>())
            CopiarAlias(item, "id", aliasId);
    }

    private static void CopiarAlias(JsonObject objeto, string destino, string origem)
    {
        if (Propriedade(objeto, destino) is not null || Propriedade(objeto, origem) is not { } valor)
            return;

        objeto[destino] = valor.DeepClone();
    }

    private static JsonElement? Propriedade(JsonElement objeto, string nome)
    {
        if (objeto.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propriedade in objeto.EnumerateObject())
            if (string.Equals(propriedade.Name, nome, StringComparison.OrdinalIgnoreCase))
                return propriedade.Value;

        return null;
    }

    private static JsonNode? Propriedade(JsonObject objeto, string nome) =>
        objeto.FirstOrDefault(p => string.Equals(p.Key, nome, StringComparison.OrdinalIgnoreCase)).Value;

    private static string? Texto(JsonElement obj, string nome) =>
        Propriedade(obj, nome) is { ValueKind: JsonValueKind.String } valor ? valor.GetString() : null;
}
