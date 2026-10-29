using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using System.Text.Json;

namespace OficinaBilling.Infrastructure.Configuration;

/// <summary>
/// Em homolog/prod lê do AWS Secrets Manager/SSM (via IRSA, sem credencial no pod) o token do
/// Mercado Pago, o segredo do webhook e a conexão com o banco. Roda uma vez no startup.
/// </summary>
public static class SecretsLoader
{
    public static Dictionary<string, string?> Carregar(SecretsOptions opts, AwsOptions aws, string? connectionStringAtual)
    {
        var valores = new Dictionary<string, string?>();
        if (!opts.Enabled)
            return valores;

        var regiao = RegionEndpoint.GetBySystemName(aws.Region);
        using var secrets = new AmazonSecretsManagerClient(regiao);

        if (!string.IsNullOrWhiteSpace(opts.MercadoPagoSecretId))
            valores["MercadoPago:AccessToken"] = Segredo(secrets, opts.MercadoPagoSecretId);

        if (!string.IsNullOrWhiteSpace(opts.WebhookSecretId))
            valores["MercadoPago:WebhookSecret"] = Segredo(secrets, opts.WebhookSecretId);

        if (string.IsNullOrWhiteSpace(connectionStringAtual) && !string.IsNullOrWhiteSpace(opts.DbSecretId))
        {
            // Segredo esperado: {"username":"...","password":"...","database":"oficina_billing"}
            using var doc = JsonDocument.Parse(Segredo(secrets, opts.DbSecretId));
            var raiz = doc.RootElement;

            using var ssm = new AmazonSimpleSystemsManagementClient(regiao);
            var host = ssm.GetParameterAsync(new GetParameterRequest { Name = opts.DbEndpointParameter! })
                .GetAwaiter().GetResult().Parameter.Value;

            valores["ConnectionStrings:BillingDb"] =
                $"Host={host};Port=5432;Database={raiz.GetProperty("database").GetString()};" +
                $"Username={raiz.GetProperty("username").GetString()};Password={raiz.GetProperty("password").GetString()};" +
                "SSL Mode=Require;Trust Server Certificate=true";
        }

        return valores;
    }

    private static string Segredo(IAmazonSecretsManager client, string id) =>
        client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = id }).GetAwaiter().GetResult().SecretString;
}
