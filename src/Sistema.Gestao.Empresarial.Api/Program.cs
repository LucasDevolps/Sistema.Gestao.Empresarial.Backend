using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Api.Health;
using Sistema.Gestao.Empresarial.Api.Errors;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application;
using Sistema.Gestao.Empresarial.Infrastructure;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Api.Auditing;

var builder = WebApplication.CreateBuilder(args);

var kestrelSecurity = builder.Configuration
    .GetRequiredSection(KestrelSecurityOptions.SectionName)
    .Get<KestrelSecurityOptions>()
    ?? throw new InvalidOperationException("A seção KestrelSecurity é obrigatória.");
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = kestrelSecurity.MaxRequestBodySizeBytes;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(kestrelSecurity.RequestHeadersTimeoutSeconds);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(kestrelSecurity.KeepAliveTimeoutSeconds);
    options.Limits.MinRequestBodyDataRate = new MinDataRate(
        kestrelSecurity.MinRequestBodyDataRateBytesPerSecond,
        TimeSpan.FromSeconds(kestrelSecurity.MinRequestBodyDataRateGracePeriodSeconds));
});

builder.Logging.AddStructuredOpenTelemetry(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, "Sistema.Gestao.Empresarial.Api");
builder.Services.AddTrustedProxy(builder.Configuration);
builder.Services.AddOptions<KestrelSecurityOptions>()
    .Bind(builder.Configuration.GetRequiredSection(KestrelSecurityOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetRequiredSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton<ApiAuditSink>();
builder.Services.AddSingleton<IApiAuditSink>(provider => provider.GetRequiredService<ApiAuditSink>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<ApiAuditSink>());
builder.Services.AddHsts(options =>
{
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

var rateLimits = builder.Configuration
    .GetRequiredSection(ApiRateLimitingOptions.SectionName)
    .Get<ApiRateLimitingOptions>()
    ?? throw new InvalidOperationException("A seção RateLimiting é obrigatória.");
builder.Services.AddOptions<ApiRateLimitingOptions>()
    .Bind(builder.Configuration.GetRequiredSection(ApiRateLimitingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"global:{ClientIdentity(context)}",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimits.GlobalPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(rateLimits.GlobalWindowSeconds)
            }));
    options.AddPolicy(RateLimitPolicies.Authentication, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"authentication:{ClientIp(context)}",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimits.AuthenticationPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(rateLimits.AuthenticationWindowSeconds)
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var correlationId = context.HttpContext.Items.TryGetValue("CorrelationId", out var value)
            ? value?.ToString()
            : null;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                title = "Limite de requisições excedido.",
                status = StatusCodes.Status429TooManyRequests,
                correlationId
            },
            cancellationToken);
    };
});

var jwt = builder.Configuration.GetRequiredSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("A seção Jwt é obrigatória.");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null
                    || !Guid.TryParse(principal.FindFirst("sub")?.Value, out var userGuid)
                    || !Guid.TryParse(principal.FindFirst("sid")?.Value, out var sessionId)
                    || string.IsNullOrWhiteSpace(principal.FindFirst("jti")?.Value)
                    || !long.TryParse(principal.FindFirst("session_version")?.Value, out var sessionVersion))
                {
                    context.Fail("Token sem identificação válida de sessão.");
                    return;
                }

                var claims = new SessionTokenClaims(
                    userGuid,
                    sessionId,
                    principal.FindFirst("jti")!.Value,
                    sessionVersion);
                var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
                if (!await validator.ValidateAsync(claims, context.HttpContext.RequestAborted))
                {
                    context.Fail("Sessão inválida, revogada ou expirada.");
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy(AuthPolicies.ActiveSession, policy => policy.RequireAuthenticatedUser());
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sistema de Gestão Empresarial Hospitalar",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Informe o access token JWT."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var sqlConnection = builder.Configuration.GetConnectionString("SqlServer")!;
var redis = builder.Configuration.GetRequiredSection(RedisOptions.SectionName).Get<RedisOptions>()!;
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddSqlServer(sqlConnection, name: "sqlserver", tags: ["ready"])
    .AddRedis(redis.Configuration, name: "redis", tags: ["ready"]);

var app = builder.Build();

var reverseProxy = app.Configuration
    .GetRequiredSection(ReverseProxyOptions.SectionName)
    .Get<ReverseProxyOptions>()!;
if (reverseProxy.Enabled)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

app.Use(async (context, next) =>
{
    var incoming = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var correlationId = Guid.TryParse(incoming, out var parsed) ? parsed : Guid.NewGuid();
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId.ToString("D");
    await next(context);
});
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<ApiRequestAuditMiddleware>();
app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = HealthResponseWriter.WriteAsync
}).RequireAuthorization();

app.Run();

static string ClientIp(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static string ClientIdentity(HttpContext context) =>
    Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userGuid)
        ? $"user:{userGuid:D}"
        : $"ip:{ClientIp(context)}";

public partial class Program;
