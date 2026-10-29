namespace OficinaBilling.Infrastructure.Configuration;

public sealed class MessagingOptions
{
    public const string Secao = "Messaging";

    /// <summary>"Aws" (SNS/SQS) ou "Log" (apenas loga — dev local sem AWS).</summary>
    public string Provider { get; set; } = "Aws";
    public string Environment { get; set; } = "dev";
    public bool ConsumerEnabled { get; set; } = true;
    public string? QueueUrl { get; set; }
    public int MaxMensagensPorLeitura { get; set; } = 10;
}

public sealed class OutboxOptions
{
    public const string Secao = "Outbox";

    public bool Enabled { get; set; } = true;
    public int IntervaloSegundos { get; set; } = 2;
    public int TamanhoLote { get; set; } = 20;
    public int MaxTentativas { get; set; } = 10;
}

public sealed class AwsOptions
{
    public const string Secao = "Aws";

    public string Region { get; set; } = "us-east-1";

    /// <summary>Preencher apenas para LocalStack (ex.: http://localstack:4566).</summary>
    public string? ServiceUrl { get; set; }
}

public sealed class MercadoPagoOptions
{
    public const string Secao = "MercadoPago";

    /// <summary>true = não chama o MP (dev/BDD). Ver FakeMercadoPagoGateway.</summary>
    public bool UseFake { get; set; }
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>true = devolve o link de sandbox (sandbox_init_point).</summary>
    public bool UseSandbox { get; set; } = true;

    /// <summary>URL pública (ALB/ngrok) que o MP chama: https://host/pagamentos/webhook/mercado-pago</summary>
    public string? NotificationUrl { get; set; }
    public string? BackUrlSucesso { get; set; }
    public string? BackUrlFalha { get; set; }
    public string? BackUrlPendente { get; set; }

    public bool ValidateSignature { get; set; }
    public string WebhookSecret { get; set; } = string.Empty;
}

public sealed class SecretsOptions
{
    public const string Secao = "Secrets";

    /// <summary>true = lê credenciais do AWS Secrets Manager/SSM no startup (homolog/prod).</summary>
    public bool Enabled { get; set; }
    public string? MercadoPagoSecretId { get; set; }
    public string? WebhookSecretId { get; set; }
    public string? DbSecretId { get; set; }
    public string? DbEndpointParameter { get; set; }
}
