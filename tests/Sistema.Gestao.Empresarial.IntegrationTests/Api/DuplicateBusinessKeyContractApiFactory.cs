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
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

/// <summary>
/// Sobe a API real (pipeline HTTP → controllers → services → GlobalExceptionHandler →
/// ProblemDetails) sobre um banco EF InMemory, com autenticação e autorização
/// substituídas por dublês. Exercita o contrato HTTP de duplicidade de ponta a ponta,
/// sem depender de SQL Server, Redis ou RabbitMQ.
/// </summary>
public class DuplicateBusinessKeyContractApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"duplicate-contract-{Guid.NewGuid():N}";
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private EmployeeSeed? _employeeSeed;

    public Guid ActorUserGuid { get; } = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

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
            // AddInfrastructure já configurou o AppDbContext para SQL Server. Removemos
            // todo o registro anterior do EF Core (options, configurations e serviços de
            // provider) antes de reconfigurar para InMemory; caso contrário o EF recusa
            // dois providers no mesmo container.
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
            services.AddScoped<IPermissionChecker, AllowAllPermissionChecker>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, options => options.UserGuid = ActorUserGuid);
        });
    }

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>
    /// Semeia (uma única vez) o grafo mínimo para <c>POST /api/funcionarios</c> — organização,
    /// unidade, catálogos, o funcionário-ator e o <see cref="Usuario"/> cujo GUID casa com o
    /// <c>sub</c> emitido pelo handler de autenticação de teste — e devolve os GUIDs do payload.
    /// </summary>
    public async Task<EmployeeSeed> SeedEmployeeGraphAsync()
    {
        if (_employeeSeed is not null)
        {
            return _employeeSeed;
        }

        await _seedLock.WaitAsync();
        try
        {
            if (_employeeSeed is not null)
            {
                return _employeeSeed;
            }

            var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var organization = new Organizacao(Guid.NewGuid(), "Rede Contrato HTTP", now);
            db.Organizacoes.Add(organization);
            await db.SaveChangesAsync();

            var hiringUnit = new UnidadeHospitalar(Guid.NewGuid(), organization.Id, "Hospital Contrato", now);
            db.UnidadesHospitalares.Add(hiringUnit);
            await db.SaveChangesAsync();

            var profession = new Profissao(Guid.NewGuid(), "Enfermagem", null, now);
            var position = new Cargo(Guid.NewGuid(), "Enfermeiro Assistencial", null, now);
            var level = new NivelProfissional(Guid.NewGuid(), "SR", "Sênior", 3, now);
            db.AddRange(profession, position, level);
            await db.SaveChangesAsync();

            var actorEmployee = new Funcionario(
                Guid.NewGuid(), "Gestor Contrato", "gestor-contrato@hospital.test", null,
                profession.Id, position.Id, level.Id, hiringUnit.Id, new DateOnly(2025, 1, 1), now);
            db.Funcionarios.Add(actorEmployee);
            await db.SaveChangesAsync();

            db.Usuarios.Add(new Usuario(ActorUserGuid, actorEmployee.Id, actorEmployee.Email, "HASH_DE_TESTE", now));
            await db.SaveChangesAsync();

            _employeeSeed = new EmployeeSeed(profession.Guid, position.Guid, level.Guid, hiringUnit.Guid);
            return _employeeSeed;
        }
        finally
        {
            _seedLock.Release();
        }
    }

    public sealed record EmployeeSeed(Guid ProfessionGuid, Guid PositionGuid, Guid LevelGuid, Guid HiringUnitGuid);

    private sealed class AllowAllPermissionChecker : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid userGuid, string permission, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userGuid, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(PermissionCodes.All));
    }

    public sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions
    {
        public Guid UserGuid { get; set; }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<TestAuthHandlerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<TestAuthHandlerOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ContractTests";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim("sub", Options.UserGuid.ToString("D"))], SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
