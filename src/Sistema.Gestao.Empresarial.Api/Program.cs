using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

builder.Logging.AddStructuredOpenTelemetry(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, "Sistema.Gestao.Empresarial.Api");
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetRequiredSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton<ISensitiveDataRedactor, SensitiveDataRedactor>();

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

app.Use(async (context, next) =>
{
    var incoming = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var correlationId = Guid.TryParse(incoming, out var parsed) ? parsed : Guid.NewGuid();
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId.ToString("D");
    await next(context);
});
app.UseMiddleware<ApiRequestAuditMiddleware>();
app.UseExceptionHandler();
if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
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

public partial class Program;
