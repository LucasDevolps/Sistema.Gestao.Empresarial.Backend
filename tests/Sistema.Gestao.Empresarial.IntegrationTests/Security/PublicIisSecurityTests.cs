using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Api.Health;
using Sistema.Gestao.Empresarial.Api.Security;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Security;

public sealed class PublicIisSecurityTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task Cloudflare_DeveUsarIpDoEdgeEIgnorarXffEHostForjados(string peer)
    {
        using var provider = CreateProxyProvider(peer);
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        var context = CreateContext(peer);
        context.Request.Headers["CF-Connecting-IP"] = "198.51.100.25";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99, 192.0.2.10";
        context.Request.Headers["X-Real-IP"] = "203.0.113.99";
        context.Request.Headers["X-Forwarded-Host"] = "untrusted.example.test";
        await Forward(context, options);

        Assert.Equal(IPAddress.Parse("198.51.100.25"), context.Connection.RemoteIpAddress);
        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal("backend.example.test", context.Request.Host.Host);
        Assert.False(TrustedProxyConfiguration.IsLocalIisRequest(context));
    }

    [Fact]
    public async Task Cloudflare_NaoDeveConfiarEmHeadersDeClienteDireto()
    {
        using var provider = CreateProxyProvider("127.0.0.1");
        var context = CreateContext("198.51.100.50");
        context.Request.Headers["CF-Connecting-IP"] = "203.0.113.99";
        await Forward(context, provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value);

        Assert.Equal(IPAddress.Parse("198.51.100.50"), context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-ip")]
    [InlineData("198.51.100.25, 203.0.113.99")]
    public async Task Cloudflare_NaoDeveUsarXffComoFallbackNemAceitarHeadersAssimétricos(string connectingIp)
    {
        using var provider = CreateProxyProvider("127.0.0.1");
        var context = CreateContext("127.0.0.1");
        context.Request.Headers["CF-Connecting-IP"] = connectingIp;
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
        await Forward(context, provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value);

        Assert.Equal(IPAddress.Loopback, context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Theory]
    [InlineData("0.0.0.0", 1)]
    [InlineData("172.30.0.2", 1)]
    [InlineData("127.0.0.1", 2)]
    public void Cloudflare_DeveRecusarTopologiaDiferenteDoConectorLocal(string proxy, int limit)
    {
        using var provider = CreateProxyProvider(proxy, limit);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ReverseProxyOptions>>().Value);
    }

    [Theory]
    [InlineData("127.0.0.1", "localhost", true)]
    [InlineData("::1", "localhost", true)]
    [InlineData("127.0.0.1", "backend.example.test", false)]
    [InlineData("198.51.100.25", "localhost", false)]
    public void ExcecaoHttpLocal_DeveExigirHostLocalEConexaoLoopback(string peer, string host, bool expected)
    {
        var context = CreateContext(peer);
        context.Request.Host = new HostString(host);
        Assert.Equal(expected, TrustedProxyConfiguration.IsLocalIisRequest(context));
    }

    [Theory]
    [InlineData("https://frontend.example.test", false, true)]
    [InlineData("https://frontend.example.test/", false, false)]
    [InlineData("https://*.example.test", false, false)]
    [InlineData("https://frontend.example.test/path", false, false)]
    [InlineData("https://frontend.example.test?query=value", false, false)]
    [InlineData("http://localhost:9080", false, false)]
    [InlineData("http://localhost:4200", true, true)]
    [InlineData("http://frontend.example.test", true, false)]
    public void Cors_DeveValidarOrigensExatasPorAmbiente(string origin, bool development, bool expected) =>
        Assert.Equal(expected, FrontendCorsConfiguration.IsValidOrigin(origin, development));

    [Fact]
    public async Task HealthPublico_NaoDeveExporInfraestruturaOuExcecoes()
    {
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>
        {
            ["internal-database"] = new(HealthStatus.Unhealthy, "internal connection details", TimeSpan.Zero,
                new InvalidOperationException("private diagnostics"), new Dictionary<string, object>())
        }, TimeSpan.Zero);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        await HealthResponseWriter.WritePublicAsync(context, report);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal("Unhealthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    private static ServiceProvider CreateProxyProvider(string proxy, int limit = 1)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Enabled"] = "true",
            ["ReverseProxy:UseCloudflareHeaders"] = "true",
            ["ReverseProxy:ForwardLimit"] = limit.ToString(),
            ["ReverseProxy:KnownProxies:0"] = proxy
        }).Build();
        return new ServiceCollection().AddTrustedProxy(configuration).BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(string peer)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("backend.example.test");
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        return context;
    }

    private static Task Forward(HttpContext context, ForwardedHeadersOptions options) =>
        new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options)).Invoke(context);
}

public sealed class PublicCorsPipelineTests : IClassFixture<SecureApiFactory>
{
    private readonly SecureApiFactory _factory;

    public PublicCorsPipelineTests(SecureApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("https://frontend.example.test", true)]
    [InlineData("https://untrusted.example.test", false)]
    [InlineData("http://localhost:9080", false)]
    public async Task Preflight_DevePermitirSomenteFrontendConfiguradoSemCookies(string origin, bool allowed)
    {
        using var factory = CreateProductionFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://hospital.test") });
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,x-correlation-id");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        if (allowed) Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Production_DeveOcultarSwaggerMesmoComFlagHabilitadaEPreservarHsts()
    {
        using var factory = CreateProductionFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://hospital.test") });
        foreach (var route in new[] { "/swagger", "/swagger/index.html", "/swagger/v1/swagger.json", "/openapi" })
        {
            using var response = await client.GetAsync(route);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Unauthorized });
        }
        using var healthRequest = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        healthRequest.Headers.Add("Origin", "https://frontend.example.test");
        using var health = await client.SendAsync(healthRequest);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("{\"status\":\"Healthy\"}", await health.Content.ReadAsStringAsync());
        Assert.True(health.Headers.Contains("Strict-Transport-Security"));
        Assert.Contains("Retry-After", string.Join(",", health.Headers.GetValues("Access-Control-Expose-Headers")));
    }

    private WebApplicationFactory<Program> CreateProductionFactory() => _factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Cors:AllowedOrigins:0", "https://frontend.example.test");
        builder.UseSetting("Swagger:Enabled", "true");
    });
}
