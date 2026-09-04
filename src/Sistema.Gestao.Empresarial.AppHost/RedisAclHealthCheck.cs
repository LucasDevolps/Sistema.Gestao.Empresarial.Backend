using System.Net.Sockets;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

internal sealed class RedisAclHealthCheck(
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
                return HealthCheckResult.Unhealthy("Redis ainda não possui endpoint ou credenciais resolvidos.");
            }

            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            await using var stream = client.GetStream();

            await WriteCommandAsync(stream, ["AUTH", user, secret], cancellationToken);
            if (!await ReadSuccessAsync(stream, cancellationToken))
            {
                return HealthCheckResult.Unhealthy("A autenticação ACL do Redis falhou.");
            }

            await WriteCommandAsync(stream, ["PING"], cancellationToken);
            return await ReadSuccessAsync(stream, cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("O Redis não respondeu ao PING autenticado.");
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("O Redis não está pronto para conexões autenticadas.", exception);
        }
    }

    private static async Task WriteCommandAsync(
        NetworkStream stream,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        await WriteAsciiAsync(stream, $"*{arguments.Count}\r\n", cancellationToken);
        foreach (var argument in arguments)
        {
            var bytes = Encoding.UTF8.GetBytes(argument);
            await WriteAsciiAsync(stream, $"${bytes.Length}\r\n", cancellationToken);
            await stream.WriteAsync(bytes, cancellationToken);
            await WriteAsciiAsync(stream, "\r\n", cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
    }

    private static async Task WriteAsciiAsync(
        NetworkStream stream,
        string value,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task<bool> ReadSuccessAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        var count = await stream.ReadAsync(buffer, cancellationToken);
        return count > 0 && buffer[0] == (byte)'+';
    }
}
