namespace Sistema.Gestao.Empresarial.IntegrationTests.Security;

public sealed class NginxConfigurationTests
{
    [Fact]
    public void Nginx_DeveSobrescreverForwardedForELimitarRequisicoes()
    {
        var root = FindRepositoryRoot();
        var nginx = File.ReadAllText(Path.Combine(root, "deploy", "nginx", "nginx.conf"));
        var proxy = File.ReadAllText(Path.Combine(root, "deploy", "nginx", "proxy-common.conf"));

        Assert.Contains("client_max_body_size 1m", nginx, StringComparison.Ordinal);
        Assert.Contains("authentication_per_ip", nginx, StringComparison.Ordinal);
        Assert.Contains("Strict-Transport-Security", nginx, StringComparison.Ordinal);
        Assert.Contains("ssl_protocols TLSv1.2 TLSv1.3", nginx, StringComparison.Ordinal);
        Assert.Contains("proxy_set_header X-Forwarded-For $remote_addr", proxy, StringComparison.Ordinal);
        Assert.DoesNotContain("$proxy_add_x_forwarded_for", proxy, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeBase_NaoDevePublicarApiNemRabbitManagementDiretamente()
    {
        var root = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));
        var apiSection = Between(compose, "  api:", "  nginx:");
        var rabbitSection = Between(compose, "  rabbitmq:", "  otel-collector:");

        Assert.DoesNotContain("ports:", apiSection, StringComparison.Ordinal);
        Assert.DoesNotContain("15672", rabbitSection, StringComparison.Ordinal);
        Assert.Contains("ReverseProxy__KnownProxies__0: 172.30.0.2", apiSection, StringComparison.Ordinal);
    }

    private static string Between(string value, string start, string end)
    {
        var startIndex = value.IndexOf(start, StringComparison.Ordinal);
        var endIndex = value.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return value[startIndex..endIndex];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sistema.Gestao.Empresarial.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Raiz da solution não encontrada.");
    }
}
