using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sistema.Gestao.Empresarial.Api.Health;

public static class HealthResponseWriter
{
    public static Task WritePublicAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { status = report.Status.ToString() }));
    }

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    durationMs = entry.Value.Duration.TotalMilliseconds,
                    description = entry.Value.Description
                })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
