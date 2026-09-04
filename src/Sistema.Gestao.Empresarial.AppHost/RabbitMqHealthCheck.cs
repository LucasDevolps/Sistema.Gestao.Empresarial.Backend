using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

internal sealed class RabbitMqHealthCheck(
    EndpointReference endpoint,
    ParameterResource username,
    ParameterResource password) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var host = await endpoint.Property(EndpointProperty.Host).GetValueAsync(cancellationToken);
            var portText = await endpoint.Property(EndpointProperty.Port).GetValueAsync(cancellationToken);
            var user = await username.GetValueAsync(cancellationToken);
            var secret = await password.GetValueAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(host) ||
                !int.TryParse(portText, out var port) ||
                string.IsNullOrEmpty(user) ||
                string.IsNullOrEmpty(secret))
            {
                return HealthCheckResult.Unhealthy("RabbitMQ ainda não possui endpoint ou credenciais resolvidos.");
            }

            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = user,
                Password = secret,
                VirtualHost = "/sge",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3)
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ recusou a conexão autenticada no vhost /sge.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                "RabbitMQ não está pronto para conexões autenticadas no vhost /sge.",
                exception);
        }
    }
}
