using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Security;

public sealed class HttpSecurityHardeningTests
{
    [Fact]
    public async Task ForwardedHeaders_DeveAceitarIpSomenteDoProxyConhecido()
    {
        var options = CreateForwardedHeadersOptions("172.30.0.2");
        IPAddress? resolvedIp = null;
        var middleware = new ForwardedHeadersMiddleware(
            context =>
            {
                resolvedIp = context.Connection.RemoteIpAddress;
                Assert.Equal("https", context.Request.Scheme);
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            Options.Create(options));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("172.30.0.2");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.25";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await middleware.Invoke(context);

        Assert.Equal(IPAddress.Parse("198.51.100.25"), resolvedIp);
    }

    [Fact]
    public async Task ForwardedHeaders_DeveIgnorarIpForjadoDeOrigemNaoConfiavel()
    {
        var options = CreateForwardedHeadersOptions("172.30.0.2");
        IPAddress? resolvedIp = null;
        var middleware = new ForwardedHeadersMiddleware(
            context =>
            {
                resolvedIp = context.Connection.RemoteIpAddress;
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            Options.Create(options));
        var context = new DefaultHttpContext();
        var directClient = IPAddress.Parse("198.51.100.50");
        context.Connection.RemoteIpAddress = directClient;
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await middleware.Invoke(context);

        Assert.Equal(directClient, resolvedIp);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Fact]
    public async Task Middleware_DeveAdicionarHeadersDeSeguranca()
    {
        var middleware = new SecurityHeadersMiddleware(async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{}");
        });
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"]);
        Assert.Contains("frame-ancestors 'none'", context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.DoesNotContain("unsafe-inline", context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.Equal("no-store, private", context.Response.Headers.CacheControl);
    }

    private static ForwardedHeadersOptions CreateForwardedHeadersOptions(string knownProxy)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Enabled"] = "true",
                ["ReverseProxy:ForwardLimit"] = "1",
                ["ReverseProxy:KnownProxies:0"] = knownProxy
            })
            .Build();
        var services = new ServiceCollection();
        services.AddTrustedProxy(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}

public sealed class ApiRateLimitingTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;

    public ApiRateLimitingTests(RateLimitedApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_DeveRetornar429ComHeadersDeSegurancaAoExcederLimitePorIp()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://hospital.test")
        });
        var request = new LoginRequest("usuario@hospital.test", "Senha forte 2026!");

        var first = await client.PostAsJsonAsync("/api/auth/login", request);
        var second = await client.PostAsJsonAsync("/api/auth/login", request);
        var rejected = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("nosniff", rejected.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", rejected.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("max-age=", rejected.Headers.GetValues("Strict-Transport-Security").Single());
        Assert.Contains("correlationId", await rejected.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}

public sealed class RateLimitedApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SqlServer", "Server=localhost;Database=sge_tests;Integrated Security=true;TrustServerCertificate=true");
        builder.UseSetting("Jwt:Issuer", "tests");
        builder.UseSetting("Jwt:Audience", "tests");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-with-at-least-thirty-two-characters");
        builder.UseSetting("Redis:Configuration", "localhost:6379,abortConnect=false");
        builder.UseSetting("Redis:InstanceName", "sge-tests");
        builder.UseSetting("RabbitMq:Host", "localhost");
        builder.UseSetting("RabbitMq:Username", "tests");
        builder.UseSetting("RabbitMq:Password", "tests");
        builder.UseSetting("OpenTelemetry:Enabled", "false");
        builder.UseSetting("RateLimiting:GlobalPermitLimit", "100");
        builder.UseSetting("RateLimiting:GlobalWindowSeconds", "300");
        builder.UseSetting("RateLimiting:AuthenticationPermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthenticationWindowSeconds", "300");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthenticationService>();
            services.AddSingleton<IAuthenticationService, RejectingAuthenticationService>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"rate-limit-{Guid.NewGuid():N}"));
        });
    }

    private sealed class RejectingAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticationResponse?> LoginAsync(
            LoginRequest request,
            AuthOperationContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult<AuthenticationResponse?>(null);

        public Task<AuthenticationResponse?> RefreshAsync(
            RefreshTokenRequest request,
            AuthOperationContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult<AuthenticationResponse?>(null);

        public Task LogoutAsync(
            SessionTokenClaims claims,
            AuthOperationContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
