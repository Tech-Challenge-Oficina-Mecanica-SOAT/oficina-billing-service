using System.Security.Cryptography;
using System.Text;

namespace OficinaBilling.Application.Security;

// Validação do header "x-signature" dos webhooks do Mercado Pago.
//
// Formato do header:   x-signature: ts=1704908010,v1=<hex hmac-sha256>
// Texto assinado:      id:<data.id>;request-id:<x-request-id>;ts:<ts>;
//   - <data.id> vem da QUERY STRING (?data.id=...), em minúsculas se for alfanumérico;
//   - partes ausentes são omitidas do texto;
//   - a chave HMAC é o "segredo da assinatura" do painel de Webhooks do MP.
// Confira a doc atual do MP se algo divergir: o formato é definido por eles.
public static class MercadoPagoWebhookSignature
{
    public static bool Validar(string? segredo, string? dataId, string? requestId, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(segredo) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        string? ts = null, v1 = null;
        foreach (var parte in signatureHeader.Split(','))
        {
            var kv = parte.Split('=', 2);
            if (kv.Length != 2) continue;

            var chave = kv[0].Trim();
            var valor = kv[1].Trim();
            if (chave == "ts") ts = valor;
            else if (chave == "v1") v1 = valor;
        }

        if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(v1))
            return false;

        var esperado = Encoding.UTF8.GetBytes(CalcularAssinatura(segredo, dataId, requestId, ts));
        var recebido = Encoding.UTF8.GetBytes(v1.ToLowerInvariant());

        return esperado.Length == recebido.Length && CryptographicOperations.FixedTimeEquals(esperado, recebido);
    }

    // Público para os testes (e para você reproduzir a assinatura nos scripts do kit).
    public static string CalcularAssinatura(string segredo, string? dataId, string? requestId, string ts)
    {
        var manifesto = new StringBuilder();

        if (!string.IsNullOrEmpty(dataId))
            manifesto.Append("id:").Append(dataId.All(char.IsAsciiLetterOrDigit) ? dataId.ToLowerInvariant() : dataId).Append(';');
        if (!string.IsNullOrEmpty(requestId))
            manifesto.Append("request-id:").Append(requestId).Append(';');
        manifesto.Append("ts:").Append(ts).Append(';');

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(segredo));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifesto.ToString()))).ToLowerInvariant();
    }
}
