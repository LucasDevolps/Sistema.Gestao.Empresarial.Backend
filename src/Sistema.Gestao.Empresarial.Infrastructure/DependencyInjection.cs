using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Infrastructure.Caching;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Messaging;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Security;
using Sistema.Gestao.Empresarial.Infrastructure.Authorization;
using Sistema.Gestao.Empresarial.Application.Employees;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using StackExchange.Redis;

namespace Sistema.Gestao.Empresarial.Infrastructure;

public static class DependencyInjection
{
    public static ILoggingBuilder AddStructuredOpenTelemetry(
        this ILoggingBuilder logging,
        IConfiguration configuration)
    {
        var options = configuration.GetRequiredSection(OpenTelemetryOptions.SectionName)
            .Get<OpenTelemetryOptions>();
        if (options?.Enabled != true)
        {
            return logging;
        }

        if (!Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("OpenTelemetry:OtlpEndpoint deve ser uma URI absoluta.");
        }

        logging.AddOpenTelemetry(openTelemetry =>
        {
            openTelemetry.IncludeScopes = true;
            openTelemetry.IncludeFormattedMessage = true;
            openTelemetry.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
        });
        return logging;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var sqlConnection = configuration.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(sqlConnection))
        {
            throw new InvalidOperationException("ConnectionStrings:SqlServer é obrigatória.");
        }

        AddValidatedOptions<SessionOptions>(services, configuration, SessionOptions.SectionName);
        AddValidatedOptions<RedisOptions>(services, configuration, RedisOptions.SectionName);
        AddValidatedOptions<CacheOptions>(services, configuration, CacheOptions.SectionName);
        AddValidatedOptions<RabbitMqOptions>(services, configuration, RabbitMqOptions.SectionName);
        AddValidatedOptions<OutboxOptions>(services, configuration, OutboxOptions.SectionName);
        AddValidatedOptions<InboxOptions>(services, configuration, InboxOptions.SectionName);
        AddValidatedOptions<AuditOptions>(services, configuration, AuditOptions.SectionName);
        AddValidatedOptions<OpenTelemetryOptions>(services, configuration, OpenTelemetryOptions.SectionName);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRedisKeyFactory, RedisKeyFactory>();
        services.AddSingleton<AuthenticationMetrics>();
        services.AddSingleton<PermissionMetrics>();
        services.AddSingleton<OutboxMetrics>();
        services.AddSingleton<InboxMetrics>();
        services.AddSingleton<IPermissionCache, PermissionCache>();
        services.AddSingleton<ICredentialHasher, CredentialHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISessionOperationalStore, RedisSessionOperationalStore>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<IAuthenticationService>(provider => provider.GetRequiredService<AuthenticationService>());
        services.AddScoped<ISessionValidator>(provider => provider.GetRequiredService<AuthenticationService>());
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<IPermissionAdministrationService, PermissionAdministrationService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(sqlConnection, sql =>
            {
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                sql.CommandTimeout(30);
            }));

        var redis = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
            ?? throw new InvalidOperationException("A seção Redis é obrigatória.");
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redis.Configuration;
            options.InstanceName = $"{redis.InstanceName}:";
        });
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redis.Configuration);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = checked(redis.ConnectTimeoutSeconds * 1000);
            return ConnectionMultiplexer.Connect(options);
        });

        AddOpenTelemetry(services, configuration, serviceName);
        return services;
    }

    private static void AddValidatedOptions<T>(
        IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where T : class
    {
        services.AddOptions<T>()
            .Bind(configuration.GetRequiredSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddOpenTelemetry(
        IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var options = configuration.GetRequiredSection(OpenTelemetryOptions.SectionName)
            .Get<OpenTelemetryOptions>()
            ?? throw new InvalidOperationException("A seção OpenTelemetry é obrigatória.");

        if (!options.Enabled)
        {
            return;
        }

        if (!Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out var endpoint))
        {
            throw new OptionsValidationException(
                OpenTelemetryOptions.SectionName,
                typeof(OpenTelemetryOptions),
                ["OpenTelemetry:OtlpEndpoint deve ser uma URI absoluta."]);
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName,
                    serviceVersion: typeof(DependencyInjection).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes([
                    new KeyValuePair<string, object>(
                        "deployment.environment",
                        configuration["DOTNET_ENVIRONMENT"] ?? "Unknown")
                ]))
            .WithTracing(tracing => tracing
                .SetSampler(new TraceIdRatioBasedSampler(options.SamplingRatio))
                .AddSource(OutboxMetrics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint))
            .WithMetrics(metrics => metrics
                .AddMeter(AuthenticationMetrics.MeterName)
                .AddMeter(PermissionMetrics.MeterName)
                .AddMeter(OutboxMetrics.MeterName)
                .AddMeter(InboxMetrics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint));
    }
}
