using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sistema.Gestao.Empresarial.Api.Auditing;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Auditing;

public sealed class HttpAuditTests
{
    [Fact]
    public async Task Middleware_DeveEnfileirarMetadadosSemCapturarCorposOuPii()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        await using var provider = services.BuildServiceProvider();
        var sink = new CapturingAuditSink();
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
            sink,
            TimeProvider.System,
            new TestHostEnvironment());
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
        var audit = Assert.Single(sink.Entries);
        Assert.Equal(StatusCodes.Status200OK, audit.StatusCode);
        Assert.Equal("/api/auth/login", audit.Endpoint);
    }

    private sealed class CapturingAuditSink : IApiAuditSink
    {
        public List<ApiAuditEntry> Entries { get; } = [];

        public bool TryWrite(ApiAuditEntry entry)
        {
            Entries.Add(entry);
            return true;
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
