using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.Messaging;
using OficinaBilling.Application.Messaging.Handlers;
using OficinaBilling.Application.UseCases.Orcamento.AprovarOrcamento;
using OficinaBilling.Application.UseCases.Orcamento.ConsultarOrcamento;
using OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;
using OficinaBilling.Application.UseCases.Orcamento.RecusarOrcamento;
using OficinaBilling.Application.UseCases.Pagamento.CancelarPorOs;
using OficinaBilling.Application.UseCases.Pagamento.ConsultarPagamento;
using OficinaBilling.Application.UseCases.Pagamento.CriarCobranca;
using OficinaBilling.Application.UseCases.Pagamento.ProcessarWebhookMercadoPago;
using OficinaBilling.Domain.Interfaces;
using OficinaBilling.Infrastructure.Configuration;
using OficinaBilling.Infrastructure.ExternalServices;
using OficinaBilling.Infrastructure.Http;
using OficinaBilling.Infrastructure.Logging;
using OficinaBilling.Infrastructure.Messaging;
using OficinaBilling.Infrastructure.Messaging.Aws;
using OficinaBilling.Infrastructure.Persistence;
using OficinaBilling.Infrastructure.Persistence.Repositories;

namespace OficinaBilling.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBilling(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<MessagingOptions>(config.GetSection(MessagingOptions.Secao));
        services.Configure<OutboxOptions>(config.GetSection(OutboxOptions.Secao));
        services.Configure<AwsOptions>(config.GetSection(AwsOptions.Secao));
        services.Configure<MercadoPagoOptions>(config.GetSection(MercadoPagoOptions.Secao));

        // ---- Persistência: database lógico "oficina_billing" ----
        services.AddDbContext<BillingDbContext>(o => o.UseNpgsql(config.GetConnectionString("BillingDb")));
        services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
        services.AddScoped<IPagamentoRepository, PagamentoRepository>();
        services.AddScoped<IOutbox, EfOutbox>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped(typeof(IAppLogger<>), typeof(AppLogger<>));

        // ---- Clientes REST (retry, timeout e circuit breaker via handler de resiliência) ----
        services.AddTransient<CorrelationIdHandler>();
        services.AddHttpClient<ICadastrosServiceClient, CadastrosServiceClient>(c =>
                c.BaseAddress = new Uri(config["Services:CadastrosService:BaseUrl"] ?? "http://localhost:8081"))
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddStandardResilienceHandler();
        services.AddHttpClient<IOsServiceClient, OsServiceClient>(c =>
                c.BaseAddress = new Uri(config["Services:OsService:BaseUrl"] ?? "http://localhost:8081"))
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddStandardResilienceHandler();

        // ---- Mercado Pago ----
        if (config.GetValue<bool>($"{MercadoPagoOptions.Secao}:UseFake"))
        {
            services.AddSingleton<FakeMercadoPagoGateway>();
            services.AddSingleton<IMercadoPagoGateway>(sp => sp.GetRequiredService<FakeMercadoPagoGateway>());
        }
        else
        {
            services.AddSingleton<IMercadoPagoGateway, MercadoPagoGateway>();
        }

        // ---- Use cases ----
        services.AddScoped<GerarOrcamentoUseCase>();
        services.AddScoped<AprovarOrcamentoUseCase>();
        services.AddScoped<RecusarOrcamentoUseCase>();
        services.AddScoped<ConsultarOrcamentoUseCase>();
        services.AddScoped<ConsultarPagamentoUseCase>();
        services.AddScoped<CriarCobrancaUseCase>();
        services.AddScoped<ProcessarWebhookMercadoPagoUseCase>();
        services.AddScoped<CancelarPorOsUseCase>();

        // ---- Handlers das mensagens SQS ----
        services.AddScoped<IMensagemHandler, DiagnosticoConcluidoHandler>();
        services.AddScoped<IMensagemHandler, ExecucaoFinalizadaHandler>();
        services.AddScoped<IMensagemHandler, OsCanceladaHandler>();

        // ---- Mensageria ----
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxProcessor>());

        var messaging = config.GetSection(MessagingOptions.Secao).Get<MessagingOptions>() ?? new MessagingOptions();
        if (messaging.Provider.Equals("Aws", StringComparison.OrdinalIgnoreCase))
        {
            var aws = config.GetSection(AwsOptions.Secao).Get<AwsOptions>() ?? new AwsOptions();

            services.AddSingleton<IAmazonSimpleNotificationService>(_ => new AmazonSimpleNotificationServiceClient(
                Configurar(new AmazonSimpleNotificationServiceConfig(), aws)));
            services.AddSingleton<IAmazonSimpleSystemsManagement>(_ => new AmazonSimpleSystemsManagementClient(
                Configurar(new AmazonSimpleSystemsManagementConfig(), aws)));
            services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(Configurar(new AmazonSQSConfig(), aws)));

            services.AddSingleton<IEventPublisher, SnsEventPublisher>();
            services.AddHostedService<SqsConsumerService>();
        }
        else
        {
            services.AddSingleton<IEventPublisher, LogEventPublisher>();
        }

        return services;
    }

    private static T Configurar<T>(T cfg, AwsOptions aws) where T : Amazon.Runtime.ClientConfig
    {
        if (!string.IsNullOrWhiteSpace(aws.ServiceUrl))
        {
            cfg.ServiceURL = aws.ServiceUrl;       // LocalStack
            cfg.AuthenticationRegion = aws.Region;
        }
        else
        {
            cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(aws.Region);
        }
        return cfg;
    }
}
