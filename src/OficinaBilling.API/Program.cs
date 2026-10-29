using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OficinaBilling.API.Extensions;
using OficinaBilling.API.Middleware;
using OficinaBilling.Infrastructure.Configuration;
using OficinaBilling.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logs em JSON no stdout: o agente do New Relic / coletor do cluster lê direto.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

// Segredos (token do Mercado Pago, webhook secret, conexão do banco) vêm do Secrets Manager em homolog/prod.
var secrets = builder.Configuration.GetSection(SecretsOptions.Secao).Get<SecretsOptions>() ?? new SecretsOptions();
var aws = builder.Configuration.GetSection(AwsOptions.Secao).Get<AwsOptions>() ?? new AwsOptions();
builder.Configuration.AddInMemoryCollection(
    SecretsLoader.Carregar(secrets, aws, builder.Configuration.GetConnectionString("BillingDb")));

builder.Services.AddBilling(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OficinaBilling API",
        Version = "v1",
        Description = "Orçamentos e pagamentos (Mercado Pago) da oficina — Billing Service da Fase 4."
    });
});

var app = builder.Build();

// ---- Banco: aplica o histórico versionado de migrations ----
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OficinaBilling API V1");
        c.RoutePrefix = "swagger";
    });
}

app.MapControllers();

// liveness: o processo está de pé. readiness: consegue falar com o banco.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "billing" }));
app.MapGet("/health/ready", async (BillingDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes de integração/BDD.
public partial class Program { }
