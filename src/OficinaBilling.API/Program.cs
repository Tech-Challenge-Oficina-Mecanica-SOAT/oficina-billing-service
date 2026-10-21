using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using Microsoft.EntityFrameworkCore;
using OficinaBilling.API;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.UseCases.Orcamento;
using OficinaBilling.Application.UseCases.Pagamento;
using OficinaBilling.Domain.Interfaces;
using OficinaBilling.Infrastructure.ExternalServices;
using OficinaBilling.Infrastructure.Logging;
using OficinaBilling.Infrastructure.Messaging;
using OficinaBilling.Infrastructure.Messaging.Consumers;
using OficinaBilling.Infrastructure.Persistence;
using OficinaBilling.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ---- Persistence: database lógico "oficina_billing" no RDS compartilhado ----
builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BillingDb")));

builder.Services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
builder.Services.AddScoped<IPagamentoRepository, PagamentoRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IIdempotenciaRepository, IdempotenciaRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ---- Logging (adapter fino p/ a Application não depender do framework) ----
builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

// ---- Clientes REST para os vizinhos (URLs vêm de ConfigMap/appsettings) ----
builder.Services.AddHttpClient<IOsServiceClient, OsServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:OsService:BaseUrl"]!));

builder.Services.AddHttpClient<ICadastrosServiceClient, CadastrosServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:CadastrosService:BaseUrl"]!));

// ---- Mercado Pago SDK ----
// Em produção o access token vem do Secrets Manager (terraform/secrets.tf);
// em dev local, de appsettings.Development.json / user-secrets.
// MercadoPago:UseFake=true troca o gateway por um em memória (teste local sem credenciais).
// Trava de segurança: nunca aceitar o fake fora de Development.
if (builder.Configuration.GetValue<bool>("MercadoPago:UseFake"))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("MercadoPago:UseFake só é permitido em Development.");

    builder.Services.AddSingleton<FakeMercadoPagoGateway>();
    builder.Services.AddSingleton<IMercadoPagoGateway>(sp => sp.GetRequiredService<FakeMercadoPagoGateway>());
}
else
{
    MercadoPagoConfig.AccessToken = builder.Configuration["MercadoPago:AccessToken"];
    builder.Services.AddSingleton(new MercadoPagoOptions(builder.Configuration["MercadoPago:NotificationUrl"]));
    builder.Services.AddScoped<PreferenceClient>();
    builder.Services.AddScoped<PaymentClient>();
    builder.Services.AddScoped<IMercadoPagoGateway, MercadoPagoGateway>();
}

// ---- AWS: SNS/SQS/SSM. Em dev local sem credenciais AWS reais, a mensageria
// fica desabilitada (ver Messaging:Enabled) e o app roda só com REST. ----
var awsRegionName = builder.Configuration["AWS:Region"] ?? "us-east-1";
var awsServiceUrl = builder.Configuration["AWS:ServiceURL"]; // preenchido só no LocalStack (ex.: http://localhost:4566)

if (!string.IsNullOrWhiteSpace(awsServiceUrl))
{
    // LocalStack aceita qualquer credencial.
    var credenciaisLocais = new BasicAWSCredentials("test", "test");
    builder.Services.AddSingleton<IAmazonSimpleNotificationService>(_ => new AmazonSimpleNotificationServiceClient(
        credenciaisLocais, new AmazonSimpleNotificationServiceConfig { ServiceURL = awsServiceUrl, AuthenticationRegion = awsRegionName }));
    builder.Services.AddSingleton<IAmazonSimpleSystemsManagement>(_ => new AmazonSimpleSystemsManagementClient(
        credenciaisLocais, new AmazonSimpleSystemsManagementConfig { ServiceURL = awsServiceUrl, AuthenticationRegion = awsRegionName }));
    builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(
        credenciaisLocais, new AmazonSQSConfig { ServiceURL = awsServiceUrl, AuthenticationRegion = awsRegionName }));
}
else
{
    var awsRegion = Amazon.RegionEndpoint.GetBySystemName(awsRegionName);
    builder.Services.AddSingleton<IAmazonSimpleNotificationService>(_ => new AmazonSimpleNotificationServiceClient(awsRegion));
    builder.Services.AddSingleton<IAmazonSimpleSystemsManagement>(_ => new AmazonSimpleSystemsManagementClient(awsRegion));
    builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(awsRegion));
}

builder.Services.AddScoped<IEventPublisher, OutboxEventPublisher>();
builder.Services.AddSingleton(sp => new TopicArnResolver(
    sp.GetRequiredService<IAmazonSimpleSystemsManagement>(),
    builder.Configuration["AWS:Environment"] ?? "dev"));

var messagingHabilitado = builder.Configuration.GetValue<bool>("Messaging:Enabled");
if (messagingHabilitado)
{
    builder.Services.AddSingleton<BillingEventDispatcher>();
    builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
    builder.Services.AddHostedService(sp => new BillingQueueConsumer(
        sp.GetRequiredService<IAmazonSQS>(),
        builder.Configuration["Messaging:QueueUrl"]!,
        sp.GetRequiredService<BillingEventDispatcher>(),
        sp.GetRequiredService<ILogger<BillingQueueConsumer>>()));
}

// ---- Use cases ----
builder.Services.AddScoped<GerarOrcamentoUseCase>();
builder.Services.AddScoped<AprovarOrcamentoUseCase>();
builder.Services.AddScoped<RecusarOrcamentoUseCase>();
builder.Services.AddScoped<ConsultarOrcamentoUseCase>();
builder.Services.AddScoped<CancelarOrcamentoPorOsUseCase>();
builder.Services.AddScoped<CriarCobrancaUseCase>();
builder.Services.AddScoped<ConsultarPagamentoUseCase>();
builder.Services.AddScoped<ProcessarWebhookMercadoPagoUseCase>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Atalho de desenvolvimento: cria as tabelas a partir do modelo do EF (sem migrations).
// Para produção gere migrations de verdade: dotnet ef migrations add InitialCreate (ver docs/GUIA-DE-TESTES.md).
if (app.Configuration.GetValue<bool>("Database:EnsureCreated"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Dev:EnableTestEndpoints"))
{
    app.MapDevEndpoints();

    // Espiar a Outbox: o que foi gravado, o que já foi publicado no SNS, quantas tentativas, último erro.
    app.MapGet("/dev/outbox", async (BillingDbContext db, CancellationToken ct) =>
        Results.Ok(await db.OutboxMessages
            .OrderBy(m => m.CriadoEm)
            .Select(m => new { m.Id, m.EventType, m.CriadoEm, m.PublicadoEm, m.Tentativas, m.UltimoErro, m.Payload })
            .ToListAsync(ct)));
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "billing" }));

app.Run();

public partial class Program { } // exposto para WebApplicationFactory nos testes de integração
