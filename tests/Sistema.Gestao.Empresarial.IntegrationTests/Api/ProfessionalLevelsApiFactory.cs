using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

/// <summary>
/// Sobe a API real (pipeline HTTP, autorização por política de permissão,
/// controllers, serviços e <c>GlobalExceptionHandler</c>) sobre EF InMemory. A
/// autenticação é um dublê dirigido pelo cabeçalho <see cref="UserHeader"/> — sem
/// ele a requisição é anônima (401) — e as permissões efetivas de cada usuário
/// vêm de <see cref="GrantPermissions"/>, exercitando 401/403 de verdade.
/// </summary>
public sealed class ProfessionalLevelsApiFactory : WebApplicationFactory<Program>
{
    public const string UserHeader = "X-Test-User";

    private readonly string _databaseName = $"professional-levels-{Guid.NewGuid():N}";
    private readonly ConcurrentDictionary<Guid, IReadOnlySet<string>> _grants = new();

    public Guid GrantPermissions(params string[] permissions)
    {
        var userGuid = Guid.NewGuid();
        _grants[userGuid] = new HashSet<string>(permissions, StringComparer.Ordinal);
        return userGuid;
    }

    public HttpClient CreateApiClient(Guid? userGuid)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (userGuid.HasValue)
        {
            client.DefaultRequestHeaders.Add(UserHeader, userGuid.Value.ToString("D"));
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SqlServer", "Server=localhost;Database=sge_tests;Integrated Security=true;TrustServerCertificate=true");
        builder.UseSetting("Jwt:Issuer", "tests");
        builder.UseSetting("Jwt:Audience", "tests");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-with-at-least-thirty-two-characters");
        builder.UseSetting("Redis:Configuration", "localhost:6379,abortConnect=false");
        builder.UseSetting("Redis:InstanceName", "sge-tests");
        builder.UseSetting("RabbitMq:Host", "localhost");
        builder.UseSetting("RabbitMq:Username", "tests");
        builder.UseSetting("RabbitMq:Password", "tests");
        builder.UseSetting("OpenTelemetry:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // Mesmo procedimento de DuplicateBusinessKeyContractApiFactory: remove todo
            // o registro do EF Core para SQL Server antes de configurar o InMemory.
            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(AppDbContext)
                             || d.ServiceType.Namespace?.StartsWith(
                                 "Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true)
                         .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IPermissionChecker>();
            services.AddSingleton<IPermissionChecker>(new GrantTablePermissionChecker(_grants));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = HeaderAuthHandler.SchemeName;
                options.DefaultChallengeScheme = HeaderAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(HeaderAuthHandler.SchemeName, _ => { });
        });
    }

    private sealed class GrantTablePermissionChecker(
        ConcurrentDictionary<Guid, IReadOnlySet<string>> grants) : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid userGuid, string permission, CancellationToken cancellationToken) =>
            Task.FromResult(grants.TryGetValue(userGuid, out var granted) && granted.Contains(permission));

        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userGuid, CancellationToken cancellationToken) =>
            Task.FromResult(grants.TryGetValue(userGuid, out var granted)
                ? granted
                : new HashSet<string>());
    }

    private sealed class HeaderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ProfessionalLevelsTests";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var value)
                || !Guid.TryParse(value.ToString(), out var userGuid))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim("sub", userGuid.ToString("D"))], SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
