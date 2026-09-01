using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Api.Auditing;

public sealed class ApiRequestAuditMiddleware(
    RequestDelegate next,
    IServiceScopeFactory scopeFactory,
    ISensitiveDataRedactor redactor,
    IOptions<AuditOptions> options,
    TimeProvider timeProvider,
    IHostEnvironment environment,
    ILogger<ApiRequestAuditMiddleware> logger)
{
    public const string ExceptionTypeItem = "ApiAudit.ExceptionType";
    private readonly AuditOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = timeProvider.GetUtcNow();
        var startedTimestamp = Stopwatch.GetTimestamp();
        var correlationId = GetCorrelationId(context);
        var requestHeaders = redactor.RedactHeaders(context.Request.Headers);
        var query = redactor.RedactQuery(context.Request.Query);
        var requestBody = await CaptureRequestBodyAsync(context.Request, context.RequestAborted);
        var originalResponseBody = context.Response.Body;
        await using var responseCapture = new BoundedCaptureStream(originalResponseBody, _options.MaxBodyBytes);
        context.Response.Body = responseCapture;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            context.Items[ExceptionTypeItem] = exception.GetType().FullName ?? exception.GetType().Name;
            throw;
        }
        finally
        {
            context.Response.Body = originalResponseBody;
            var finishedAt = timeProvider.GetUtcNow();
            var responseBody = redactor.RedactBody(
                Encoding.UTF8.GetString(responseCapture.GetCapturedBytes()),
                context.Response.ContentType,
                responseCapture.IsTruncated);

            await PersistAsync(
                context,
                startedAt,
                finishedAt,
                (long)Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                correlationId,
                query,
                requestHeaders,
                requestBody,
                redactor.RedactHeaders(context.Response.Headers),
                responseBody);
        }
    }

    private async Task<string?> CaptureRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength == 0)
            return null;

        request.EnableBuffering();
        var buffer = ArrayPool<byte>.Shared.Rent(_options.MaxBodyBytes + 1);
        try
        {
            var read = 0;
            while (read <= _options.MaxBodyBytes)
            {
                var current = await request.Body.ReadAsync(
                    buffer.AsMemory(read, _options.MaxBodyBytes + 1 - read), cancellationToken);
                if (current == 0)
                    break;
                read += current;
            }
            request.Body.Position = 0;
            var truncated = read > _options.MaxBodyBytes;
            var capturedLength = Math.Min(read, _options.MaxBodyBytes);
            return redactor.RedactBody(
                Encoding.UTF8.GetString(buffer, 0, capturedLength),
                request.ContentType,
                truncated);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private async Task PersistAsync(
        HttpContext context,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        long elapsedMilliseconds,
        Guid correlationId,
        string? query,
        string requestHeaders,
        string? requestBody,
        string responseHeaders,
        string? responseBody)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.PersistenceTimeoutSeconds));
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userGuid = Guid.TryParse(context.User.FindFirst("sub")?.Value, out var parsedUserGuid)
                ? parsedUserGuid
                : (Guid?)null;
            long? userId = null;
            if (userGuid.HasValue)
            {
                userId = await dbContext.Usuarios.AsNoTracking()
                    .Where(x => x.Guid == userGuid.Value)
                    .Select(x => (long?)x.Id)
                    .SingleOrDefaultAsync(timeout.Token);
            }

            var traceId = Activity.Current?.TraceId.ToString();
            if (string.IsNullOrWhiteSpace(traceId))
                traceId = context.TraceIdentifier;
            if (string.IsNullOrWhiteSpace(traceId))
                traceId = correlationId.ToString("N");

            dbContext.ApiRequestLogs.Add(new ApiRequestLog(
                Guid.NewGuid(),
                startedAt,
                finishedAt,
                context.Request.Method,
                Limit($"{context.Request.PathBase}{context.Request.Path}", 2048) ?? "/",
                query,
                requestHeaders,
                requestBody,
                responseHeaders,
                responseBody,
                context.Response.StatusCode,
                elapsedMilliseconds,
                context.Response.StatusCode is >= 200 and < 400,
                correlationId,
                Limit(traceId, 64)!,
                Limit(context.Connection.RemoteIpAddress?.ToString(), 64),
                Limit(context.Request.Headers.UserAgent.ToString(), 1000),
                userId,
                userGuid,
                Limit(environment.EnvironmentName, 100) ?? "Unknown",
                Limit(context.Items[ExceptionTypeItem]?.ToString(), 1000)));
            await dbContext.SaveChangesAsync(timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao persistir auditoria HTTP da correlação {CorrelationId}",
                correlationId);
        }
    }

    private static Guid GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue("CorrelationId", out var value) && value is Guid correlationId
            ? correlationId
            : Guid.NewGuid();

    private static string? Limit(string? value, int maximumLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
