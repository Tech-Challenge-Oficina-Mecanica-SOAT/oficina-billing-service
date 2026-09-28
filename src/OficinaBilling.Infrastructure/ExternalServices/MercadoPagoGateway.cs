using OficinaBilling.Application.Interfaces;

namespace OficinaBilling.Infrastructure.ExternalServices;

// TODO: envolver o SDK oficial do Mercado Pago aqui dentro.
// O access token vem do Secrets Manager (ver terraform/secrets.tf), nunca hardcoded.
public class MercadoPagoGateway : IMercadoPagoGateway
{
    public Task<CobrancaCriada> CriarCobrancaAsync(decimal valor, string descricao, CancellationToken ct = default)
    {
        throw new NotImplementedException("Integrar com o SDK do Mercado Pago aqui.");
    }
}
