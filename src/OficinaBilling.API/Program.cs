using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OficinaBilling.Application.Interfaces;
using OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;
using OficinaBilling.Application.UseCases.Pagamento.ProcessarWebhookMercadoPago;
using OficinaBilling.Domain.Interfaces;
using OficinaBilling.Infrastructure.ExternalServices;
using OficinaBilling.Infrastructure.Persistence;
using OficinaBilling.Infrastructure.Persistence.Repositories;
using OficinaBilling.Infrastructure.EventPublisher;

var builder = WebApplication.CreateBuilder(args);

// ---- Persistence: database lógico "oficina_billing" no RDS compartilhado ----
builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BillingDb")));

builder.Services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
builder.Services.AddScoped<IPagamentoRepository, PagamentoRepository>();

// ---- Clientes REST para os vizinhos (URLs vêm de ConfigMap/appsettings) ----
builder.Services.AddHttpClient<IOsServiceClient, OsServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:OsService:BaseUrl"]!));

builder.Services.AddHttpClient<ICadastrosServiceClient, CadastrosServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:CadastrosService:BaseUrl"]!));

// ---- Gateway de pagamento e mensageria ----
builder.Services.AddScoped<IMercadoPagoGateway, MercadoPagoGateway>();
// TODO: registrar IEventPublisher (SnsEventPublisher) e os clientes AWS (SNS/SQS/SSM)
// ---- Event Publisher (dummy para desenvolvimento) ----
builder.Services.AddScoped<IEventPublisher, DummyEventPublisher>();

// ---- Use cases ----
builder.Services.AddScoped<GerarOrcamentoUseCase>();
builder.Services.AddScoped<ProcessarWebhookMercadoPagoUseCase>();
// TODO: registrar os demais use cases (Aprovar/Recusar/Consultar/CriarCobranca)

builder.Services.AddControllers();

// ---- Configuração do Swagger/OpenAPI para .NET 10 ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OficinaBilling API",
        Version = "v1",
        Description = "API para gestão de cobranças do sistema de oficina",
        Contact = new OpenApiContact
        {
            Name = "OficinaBilling Team",
            Email = "suporte@oficinabilling.com"
        }
    });
});

var app = builder.Build();

// ---- Configuração do pipeline ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OficinaBilling API V1");
        c.RoutePrefix = "swagger"; // Para acessar em /swagger
    });
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "billing" }));

app.Run();