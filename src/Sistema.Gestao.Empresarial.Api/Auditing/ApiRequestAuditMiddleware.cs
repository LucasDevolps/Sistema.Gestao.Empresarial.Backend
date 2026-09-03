using System.Diagnostics;

namespace Sistema.Gestao.Empresarial.Api.Auditing;

public sealed class ApiRequestAuditMiddleware(
    RequestDelegate next,
    IApiAuditSink sink,
    TimeProvider timeProvider,
    IHostEnvironment environment)
{
    public const string ExceptionTypeItem = "ApiAudit.ExceptionType";

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = timeProvider.GetUtcNow();
        var startedTimestamp = Stopwatch.GetTimestamp();
        var correlationId = GetCorrelationId(context);

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
            var traceId = Activity.Current?.TraceId.ToString();
            if (string.IsNullOrWhiteSpace(traceId))
            {
                traceId = string.IsNullOrWhiteSpace(context.TraceIdentifier)
                    ? correlationId.ToString("N")
                    : context.TraceIdentifier;
            }

            var userGuid = Guid.TryParse(context.User.FindFirst("sub")?.Value, out var parsedUserGuid)
                ? parsedUserGuid
                : (Guid?)null;
            sink.TryWrite(new ApiAuditEntry(
                startedAt,
                timeProvider.GetUtcNow(),
                context.Request.Method,
                Limit($"{context.Request.PathBase}{context.Request.Path}", 2048) ?? "/",
                context.Response.StatusCode,
                (long)Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                correlationId,
                Limit(traceId, 64)!,
                userGuid,
                Limit(environment.EnvironmentName, 100) ?? "Unknown",
                Limit(context.Items[ExceptionTypeItem]?.ToString(), 1000)));
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
