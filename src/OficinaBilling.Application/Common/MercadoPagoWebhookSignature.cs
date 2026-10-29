using System.Security.Cryptography;
using System.Text;

namespace OficinaBilling.Application.Common;

/// <summary>
/// Valida o header <c>x-signature</c> das notificações do Mercado Pago:
/// HMAC-SHA256 (chave = segredo do webhook) sobre o manifesto
/// <c>id:{data.id};request-id:{x-request-id};ts:{ts};</c>.
/// </summary>
public static class MercadoPagoWebhookSignature
{
    public static bool Validar(string segredo, string? xSignature, string? xRequestId, string? dataId)
    {
        if (string.IsNullOrWhiteSpace(segredo) || string.IsNullOrWhiteSpace(xSignature))
            return false;

        string? ts = null, v1 = null;
        foreach (var parte in xSignature.Split(','))
        {
            var kv = parte.Split('=', 2);
            if (kv.Length != 2) continue;
            var chave = kv[0].Trim();
            if (chave == "ts") ts = kv[1].Trim();
            else if (chave == "v1") v1 = kv[1].Trim();
        }

        if (ts is null || v1 is null)
            return false;

        // A documentação do MP exige o data.id em minúsculas quando alfanumérico.
        var id = (dataId ?? string.Empty).ToLowerInvariant();
        var manifesto = $"id:{id};request-id:{xRequestId};ts:{ts};";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(segredo));
        var calculado = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifesto))).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(calculado),
            Encoding.UTF8.GetBytes(v1.ToLowerInvariant()));
    }
}
