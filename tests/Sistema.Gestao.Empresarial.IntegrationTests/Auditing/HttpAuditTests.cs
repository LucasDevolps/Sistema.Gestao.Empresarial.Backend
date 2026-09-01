using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Sistema.Gestao.Empresarial.Api.Auditing;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Auditing;

public sealed class HttpAuditTests
{
    [Fact]
    public void Redator_DeveMascararDadosSensiveisRecursivamente()
    {
        var redactor = new SensitiveDataRedactor();
        const string body = """
            {
              "email":"usuario@hospital.test",
              "senha":"segredo-1",
              "nested":{"accessToken":"segredo-2"},
              "items":[{"client_secret":"segredo-3"}],
              "note":"Bearer segredo-4"
            }
            """;

        var result = redactor.RedactBody(body, "application/json; charset=utf-8", truncated: false)!;

        Assert.Contains("usuario@hospital.test", result, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo-1", result, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo-2", result, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo-3", result, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo-4", result, StringComparison.Ordinal);
        Assert.Contains(SensitiveDataRedactor.Redacted, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Redator_DeveMascararHeadersQueryEBodyTruncado()
    {
        var redactor = new SensitiveDataRedactor();
        var headers = new HeaderDictionary
        {
            ["Authorization"] = "Bearer token-real",
            ["Cookie"] = "session=segredo",
            ["User-Agent"] = "hospital-client"
        };
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["api_key"] = "chave-real",
            ["page"] = "2"
        });

        var redactedHeaders = redactor.RedactHeaders(headers);
        var redactedQuery = redactor.RedactQuery(query)!;

        Assert.DoesNotContain("token-real", redactedHeaders, StringComparison.Ordinal);
        Assert.DoesNotContain("session=segredo", redactedHeaders, StringComparison.Ordinal);
        Assert.Contains("hospital-client", redactedHeaders, StringComparison.Ordinal);
        Assert.DoesNotContain("chave-real", redactedQuery, StringComparison.Ordinal);
        Assert.Contains("\"page\":\"2\"", redactedQuery, StringComparison.Ordinal);
        Assert.Equal(
            SensitiveDataRedactor.Truncated,
            redactor.RedactBody("{\"password\":\"parcial", "application/json", truncated: true));
    }

    [Fact]
    public async Task Middleware_DevePersistirRequestEResponseMascaradosSemConsumirRequest()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        var redactor = new SensitiveDataRedactor();
        string? bodyReadByEndpoint = null;
        RequestDelegate next = async context =>
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            bodyReadByEndpoint = await reader.ReadToEndAsync(context.RequestAborted);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync("{\"accessToken\":\"response-secret\",\"ok\":true}");
        };
        var middleware = new ApiRequestAuditMiddleware(
            next,
            provider.GetRequiredService<IServiceScopeFactory>(),
            redactor,
            Options.Create(new AuditOptions { MaxBodyBytes = 4096, PersistenceTimeoutSeconds = 5 }),
            TimeProvider.System,
            new TestHostEnvironment(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ApiRequestAuditMiddleware>>());
        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Items["CorrelationId"] = Guid.NewGuid();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/auth/login";
        context.Request.ContentType = "application/json";
        context.Request.Headers.Authorization = "Bearer request-token";
        const string requestJson = "{\"email\":\"user@test\",\"password\":\"request-secret\"}";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson));
        context.Request.ContentLength = context.Request.Body.Length;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(requestJson, bodyReadByEndpoint);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.ApiRequestLogs.SingleAsync();
        Assert.DoesNotContain("request-secret", audit.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("request-token", audit.RequestHeaders, StringComparison.Ordinal);
        Assert.DoesNotContain("response-secret", audit.ResponseBody, StringComparison.Ordinal);
        Assert.Contains("user@test", audit.RequestBody, StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status200OK, audit.StatusCode);
        Assert.True(audit.Sucesso);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
