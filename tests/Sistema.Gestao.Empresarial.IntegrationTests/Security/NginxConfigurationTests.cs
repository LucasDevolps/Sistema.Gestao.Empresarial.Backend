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
        Assert.Contains("return 308 https://__SERVER_NAME__:8443$request_uri", nginx, StringComparison.Ordinal);
        Assert.DoesNotContain("https://$host", nginx, StringComparison.Ordinal);
        Assert.Contains("listen 8080 default_server", nginx, StringComparison.Ordinal);
        Assert.Contains("listen 8443 ssl default_server", nginx, StringComparison.Ordinal);
        Assert.Contains("location = /health/ready", nginx, StringComparison.Ordinal);
        Assert.Contains("Cache-Control \"no-store, private\"", nginx, StringComparison.Ordinal);
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
        Assert.Contains("ReverseProxy__KnownProxies__0: ${SGE_NGINX_INTERNAL_IP:-172.30.0.2}", apiSection, StringComparison.Ordinal);

        var developmentOverride = File.ReadAllText(Path.Combine(root, "docker-compose.override.yml"));
        Assert.DoesNotContain("1433:1433", developmentOverride, StringComparison.Ordinal);
        Assert.DoesNotContain("6379:6379", developmentOverride, StringComparison.Ordinal);
        Assert.DoesNotContain("15672:15672", developmentOverride, StringComparison.Ordinal);
        Assert.DoesNotContain("4317:4317", developmentOverride, StringComparison.Ordinal);
        Assert.Contains("DOTNET_ENVIRONMENT: Development", developmentOverride, StringComparison.Ordinal);
        Assert.Contains("DOTNET_ENVIRONMENT: Production", compose, StringComparison.Ordinal);
        Assert.Contains("--aclfile /tmp/users.acl", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Ci_DeveTestarProxyComHostPermitidoSemExporHealthChecksDaAplicacao()
    {
        var root = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        Assert.Contains("--header=\"Host: ${NGINX_SERVER_NAME}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("https://127.0.0.1:8443/nginx-health", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("https://127.0.0.1:8443/health/live", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("https://127.0.0.1:8443/health/ready", workflow, StringComparison.Ordinal);
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
