using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sistema.Gestao.Empresarial.Application;
using Sistema.Gestao.Empresarial.Application.Integration;
using Sistema.Gestao.Empresarial.Infrastructure;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Health;
using Sistema.Gestao.Empresarial.Infrastructure.Messaging;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddStructuredOpenTelemetry(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, "Sistema.Gestao.Empresarial.Worker");

var rabbit = builder.Configuration.GetRequiredSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
    ?? throw new InvalidOperationException("A seção RabbitMq é obrigatória.");
builder.Services.AddMassTransit(configurator =>
{
    configurator.ConfigureHealthCheckOptions(options => options.Tags.Add("ready"));
    configurator.UsingRabbitMq((context, bus) =>
    {
        bus.Host(rabbit.Host, (ushort)rabbit.Port, rabbit.VirtualHost, host =>
        {
            host.Username(rabbit.Username);
            host.Password(rabbit.Password);
        });
        bus.ConfigureEndpoints(context);
    });
});
builder.Services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();
builder.Services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<AppDbContext>("sqlserver", tags: ["ready"])
    .AddCheck<RedisReadinessHealthCheck>("redis", tags: ["ready"]);

var app = builder.Build();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();
