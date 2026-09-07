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
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml")).ReplaceLineEndings("\n");
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

        var ciOverride = File.ReadAllText(Path.Combine(root, "docker-compose.ci.yml")).ReplaceLineEndings("\n");
        Assert.Contains("127.0.0.1:1433:1433", ciOverride, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:6379:6379", ciOverride, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:5672:5672", ciOverride, StringComparison.Ordinal);
        Assert.Contains("default:\n    internal: false", ciOverride, StringComparison.Ordinal);
        Assert.Contains(
            "default:\n    name: ${SGE_DOCKER_NETWORK:-sge-network}\n    internal: true",
            compose,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeLocal_DevePublicarNginxEDashboardSomenteEmLoopback()
    {
        var root = FindRepositoryRoot();
        var developmentOverride = File.ReadAllText(Path.Combine(root, "docker-compose.override.yml")).ReplaceLineEndings("\n");
        var nginx = Between(developmentOverride, "\n  nginx:\n", "\n  otel-collector:\n");
        var dashboard = Between(developmentOverride, "\n  aspire-dashboard:\n", "\nnetworks:\n");

        Assert.Equal(
            ["127.0.0.1:${SGE_HTTP_PORT:-8080}:8080", "127.0.0.1:${SGE_HTTPS_PORT:-8443}:8443"],
            PublishedPorts(nginx));
        // A lista completa impede publicação adicional de qualquer listener OTLP.
        Assert.Equal(["127.0.0.1:18888:18888"], PublishedPorts(dashboard));
    }

    [Fact]
    public void Aspire_DeveExigirAutenticacaoNaUiENaIngestao()
    {
        var root = FindRepositoryRoot();
        var developmentOverride = File.ReadAllText(Path.Combine(root, "docker-compose.override.yml")).ReplaceLineEndings("\n");
        var collector = Between(developmentOverride, "\n  otel-collector:\n", "\n  aspire-dashboard:\n");
        var dashboard = Between(developmentOverride, "\n  aspire-dashboard:\n", "\nnetworks:\n");
        var exporter = File.ReadAllText(Path.Combine(root, "deploy", "otel-collector-aspire.yml"));

        Assert.Contains("DASHBOARD__FRONTEND__AUTHMODE: BrowserToken", dashboard, StringComparison.Ordinal);
        Assert.Contains("DASHBOARD__OTLP__AUTHMODE: ApiKey", dashboard, StringComparison.Ordinal);
        Assert.Contains("DASHBOARD__OTLP__PRIMARYAPIKEY: ${SGE_ASPIRE_OTLP_API_KEY:?", dashboard, StringComparison.Ordinal);
        Assert.Contains("SGE_ASPIRE_OTLP_API_KEY: ${SGE_ASPIRE_OTLP_API_KEY:?", collector, StringComparison.Ordinal);
        Assert.Contains("x-otlp-api-key: ${env:SGE_ASPIRE_OTLP_API_KEY}", exporter, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("docker-compose.yml")]
    [InlineData("docker-compose.production.yml")]
    [InlineData("docker-compose.ci.yml")]
    public void ComposeBaseProducaoECi_NaoDevemIncluirDashboardLocal(string fileName)
    {
        var compose = File.ReadAllText(Path.Combine(FindRepositoryRoot(), fileName));

        Assert.DoesNotContain("aspire-dashboard", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("otel-collector-aspire", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("SGE_ASPIRE_OTLP_API_KEY", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("dashboard-access", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("docker-compose.override.yml", compose, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".gitignore")]
    [InlineData(".dockerignore")]
    public void CredenciaisLocais_DevemSerExcluidasDoGitEDoContextoDocker(string fileName)
    {
        var rules = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), fileName));

        Assert.Contains(".env", rules);
        Assert.Contains(".env.*", rules);
        Assert.Contains("!.env.example", rules);
        Assert.Contains("CREDENCIAIS-DEV-LOCAL.md", rules);
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

    private static string[] PublishedPorts(string serviceSection)
    {
        var lines = serviceSection.Split('\n');
        var portsIndex = Array.IndexOf(lines, "    ports:");
        Assert.True(portsIndex >= 0, "A seção do serviço deve declarar suas portas publicadas.");

        return lines.Skip(portsIndex + 1)
            .TakeWhile(line => string.IsNullOrWhiteSpace(line) || line.StartsWith("      ", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.TrimStart('-', ' ').Trim('"', '\''))
            .ToArray();
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
