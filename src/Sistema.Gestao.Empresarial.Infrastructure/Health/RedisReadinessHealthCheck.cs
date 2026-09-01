using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Sistema.Gestao.Empresarial.Infrastructure.Health;

public sealed class RedisReadinessHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis respondeu em {latency.TotalMilliseconds:F0} ms.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis indisponível.", exception);
        }
    }
}
