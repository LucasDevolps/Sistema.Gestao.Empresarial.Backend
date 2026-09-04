using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

var sqlSaPassword = builder.AddParameterFromConfiguration(
    "sqlserver-sa-password",
    "SGE_SQLSERVER_SA_PASSWORD",
    secret: true);
var sqlAppUsername = builder.AddParameterFromConfiguration(
    "sqlserver-app-username",
    "SGE_SQLSERVER_APP_USERNAME");
var sqlAppPassword = builder.AddParameterFromConfiguration(
    "sqlserver-app-password",
    "SGE_SQLSERVER_APP_PASSWORD",
    secret: true);
var redisUsername = builder.AddParameterFromConfiguration(
    "redis-username",
    "SGE_REDIS_USERNAME");
var redisPassword = builder.AddParameterFromConfiguration(
    "redis-password",
    "SGE_REDIS_PASSWORD",
    secret: true);
var rabbitUsername = builder.AddParameterFromConfiguration(
    "rabbitmq-username",
    "SGE_RABBITMQ_USERNAME");
var rabbitPassword = builder.AddParameterFromConfiguration(
    "rabbitmq-password",
    "SGE_RABBITMQ_PASSWORD",
    secret: true);
var jwtSigningKey = builder.AddParameterFromConfiguration(
    "jwt-signing-key",
    "SGE_JWT_SIGNING_KEY",
    secret: true);

var sqlServer = builder.AddSqlServer("sqlserver", sqlSaPassword)
    .WithImageTag("2022-latest@sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89")
    .WithVolume("sge-aspire-sql-data", "/var/opt/mssql");

var sqlEndpoint = sqlServer.GetEndpoint("tcp");
var sqlAdminConnection = ReferenceExpression.Create(
    $"Server={sqlEndpoint.Property(EndpointProperty.Host)},{sqlEndpoint.Property(EndpointProperty.Port)};Database=SistemaGestaoEmpresarial;User Id=sa;Password=\"{sqlSaPassword}\";Encrypt=True;TrustServerCertificate=True");
var sqlAppConnection = ReferenceExpression.Create(
    $"Server={sqlEndpoint.Property(EndpointProperty.Host)},{sqlEndpoint.Property(EndpointProperty.Port)};Database=SistemaGestaoEmpresarial;User Id={sqlAppUsername};Password=\"{sqlAppPassword}\";Encrypt=True;TrustServerCertificate=True");

var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", ".."));
var sqlInitScript = Path.Combine(repositoryRoot, "deploy", "sqlserver", "initialize-app-login.sh");
var sqlInit = builder.AddContainer(
        "sqlserver-init",
        "mcr.microsoft.com/mssql/server",
        "2022-latest@sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89")
    .WithBindMount(sqlInitScript, "/usr/local/bin/initialize-app-login.sh", isReadOnly: true)
    .WithEntrypoint("/bin/bash")
    .WithArgs("/usr/local/bin/initialize-app-login.sh")
    .WithEnvironment("SGE_SQLSERVER_SA_PASSWORD", sqlSaPassword)
    .WithEnvironment("SGE_SQLSERVER_APP_USERNAME", sqlAppUsername)
    .WithEnvironment("SGE_SQLSERVER_APP_PASSWORD", sqlAppPassword)
    .WaitFor(sqlServer);

var dotnetToolRestore = builder.AddExecutable(
        "dotnet-tools-restore",
        "dotnet",
        repositoryRoot,
        "tool", "restore")
    .WaitForCompletion(sqlInit);

var migrations = builder.AddExecutable(
        "database-migrations",
        "dotnet",
        repositoryRoot,
        "tool", "run", "dotnet-ef", "database", "update",
        "--project", "src/Sistema.Gestao.Empresarial.Infrastructure",
        "--startup-project", "src/Sistema.Gestao.Empresarial.Infrastructure",
        "--context", "AppDbContext",
        "--no-build")
    .WithEnvironment("SGE_DESIGNTIME_SQLSERVER", sqlAdminConnection)
    .WaitForCompletion(dotnetToolRestore);

var redis = builder.AddContainer(
        "redis",
        "redis",
        "8.2.1-alpine@sha256:987c376c727652f99625c7d205a1cba3cb2c53b92b0b62aade2bd48ee1593232")
    .WithEnvironment("REDIS_USERNAME", redisUsername)
    .WithEnvironment("REDIS_PASSWORD", redisPassword)
    .WithArgs(
        "/bin/sh",
        "-ec",
        "case \"${REDIS_USERNAME}\" in *[!A-Za-z0-9_-]*|'') exit 64;; esac\n" +
        "password_hash=\"$(printf '%s' \"${REDIS_PASSWORD}\" | sha256sum | cut -d' ' -f1)\"\n" +
        "printf 'user default off\\nuser %s on #%s ~sge* +@read +@write +@scripting +ping +client +info +echo +command\\n' \"${REDIS_USERNAME}\" \"${password_hash}\" > /tmp/users.acl\n" +
        "exec redis-server --appendonly yes --aclfile /tmp/users.acl")
    .WithEndpoint(targetPort: 6379, name: "tcp")
    .WithVolume("sge-aspire-redis-data", "/data");

const string redisHealthCheckName = "redis-acl";
builder.Services.AddHealthChecks().AddCheck(
    redisHealthCheckName,
    new RedisAclHealthCheck(redis.GetEndpoint("tcp"), redisUsername.Resource, redisPassword.Resource));
redis.WithHealthCheck(redisHealthCheckName);

var redisEndpoint = redis.GetEndpoint("tcp");
var redisConfiguration = ReferenceExpression.Create(
    $"{redisEndpoint.Property(EndpointProperty.Host)}:{redisEndpoint.Property(EndpointProperty.Port)},user={redisUsername},password={redisPassword},abortConnect=false");

var rabbitMq = builder.AddContainer(
        "rabbitmq",
        "rabbitmq",
        "4.3.5-management-alpine@sha256:5b6a50b2f1dbd987bb1a6a9e20b152910c3dc8ae32e1c9060b543ecd9250f6b9")
    .WithEnvironment("RABBITMQ_DEFAULT_USER", rabbitUsername)
    .WithEnvironment("RABBITMQ_DEFAULT_PASS", rabbitPassword)
    .WithEnvironment("RABBITMQ_DEFAULT_VHOST", "/sge")
    .WithEndpoint(targetPort: 5672, name: "tcp")
    .WithVolume("sge-aspire-rabbitmq-data", "/var/lib/rabbitmq");

var rabbitEndpoint = rabbitMq.GetEndpoint("tcp");
const string rabbitHealthCheckName = "rabbitmq-authenticated";
builder.Services.AddHealthChecks().AddCheck(
    rabbitHealthCheckName,
    new RabbitMqHealthCheck(rabbitEndpoint, rabbitUsername.Resource, rabbitPassword.Resource));
rabbitMq.WithHealthCheck(rabbitHealthCheckName);

var dashboardOtlpEndpoint = builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"]
    ?? throw new InvalidOperationException("O endpoint OTLP do Aspire Dashboard não foi configurado.");

var api = builder.AddProject<Projects.Sistema_Gestao_Empresarial_Api>("api", launchProfileName: "https")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("ConnectionStrings__SqlServer", sqlAppConnection)
    .WithEnvironment("Redis__Configuration", redisConfiguration)
    .WithEnvironment("RabbitMq__Host", rabbitEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RabbitMq__Port", rabbitEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("RabbitMq__VirtualHost", "/sge")
    .WithEnvironment("RabbitMq__Username", rabbitUsername)
    .WithEnvironment("RabbitMq__Password", rabbitPassword)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("OpenTelemetry__Enabled", "true")
    .WithEnvironment("OpenTelemetry__OtlpEndpoint", dashboardOtlpEndpoint)
    .WithEnvironment("OpenTelemetry__SamplingRatio", "1.0")
    .WithEnvironment("ReverseProxy__Enabled", "false")
    .WithHttpHealthCheck("/health/ready")
    .WaitForCompletion(migrations)
    .WaitFor(redis);

builder.AddProject<Projects.Sistema_Gestao_Empresarial_Worker>("worker")
    .WithHttpEndpoint(env: "ASPNETCORE_HTTP_PORTS", name: "http")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("ConnectionStrings__SqlServer", sqlAppConnection)
    .WithEnvironment("Redis__Configuration", redisConfiguration)
    .WithEnvironment("RabbitMq__Host", rabbitEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RabbitMq__Port", rabbitEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("RabbitMq__VirtualHost", "/sge")
    .WithEnvironment("RabbitMq__Username", rabbitUsername)
    .WithEnvironment("RabbitMq__Password", rabbitPassword)
    .WithEnvironment("OpenTelemetry__Enabled", "true")
    .WithEnvironment("OpenTelemetry__OtlpEndpoint", dashboardOtlpEndpoint)
    .WithEnvironment("OpenTelemetry__SamplingRatio", "1.0")
    .WithHttpHealthCheck("/health/ready")
    .WaitForCompletion(migrations)
    .WaitFor(redis)
    .WaitFor(rabbitMq);

builder.Build().Run();
