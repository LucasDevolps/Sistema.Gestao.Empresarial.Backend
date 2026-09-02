using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sistema.Gestao.Empresarial.Application.Employees;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealInfrastructureCollection : ICollectionFixture<RealInfrastructureFixture>
{
    public const string Name = "RealInfrastructure";
}

public sealed partial class RealInfrastructureFixture : IAsyncLifetime
{
    private string _masterConnectionString = string.Empty;
    private ServiceProvider? _metricsProvider;

    public string DatabaseName { get; } = $"sge_it_{Guid.NewGuid():N}";
    public string DatabaseConnectionString { get; private set; } = string.Empty;
    public string RedisConfiguration { get; private set; } = string.Empty;
    public string RabbitMqHost { get; private set; } = string.Empty;
    public ushort RabbitMqPort { get; private set; }
    public string RabbitMqUsername { get; private set; } = string.Empty;
    public string RabbitMqPassword { get; private set; } = string.Empty;
    public string IsolationKey { get; } = Guid.NewGuid().ToString("N");
    public IConnectionMultiplexer Redis { get; private set; } = null!;
    public IServiceProvider Metrics => _metricsProvider!;

    public Guid ProfessionGuid { get; private set; }
    public Guid PositionGuid { get; private set; }
    public Guid LevelGuid { get; } = Guid.Parse("E6E15AE5-FF9B-4A07-884A-5E66F805BFE0");
    public Guid HiringUnitGuid { get; private set; }
    public Guid ActingUnitGuid { get; private set; }
    public Guid SectorGuid { get; private set; }

    public async Task InitializeAsync()
    {
        _masterConnectionString = RequiredEnvironment("SGE_TEST_SQLSERVER");
        RedisConfiguration = RequiredEnvironment("SGE_TEST_REDIS");
        RabbitMqHost = RequiredEnvironment("SGE_TEST_RABBITMQ_HOST");
        RabbitMqPort = ushort.Parse(RequiredEnvironment("SGE_TEST_RABBITMQ_PORT"));
        RabbitMqUsername = RequiredEnvironment("SGE_TEST_RABBITMQ_USERNAME");
        RabbitMqPassword = RequiredEnvironment("SGE_TEST_RABBITMQ_PASSWORD");

        ValidateDatabaseName(DatabaseName);
        var masterBuilder = new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = "master"
        };
        _masterConnectionString = masterBuilder.ConnectionString;
        var databaseBuilder = new SqlConnectionStringBuilder(masterBuilder.ConnectionString)
        {
            InitialCatalog = DatabaseName
        };
        DatabaseConnectionString = databaseBuilder.ConnectionString;

        await using (var connection = new SqlConnection(_masterConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{DatabaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
            await SeedAsync(db);
        }

        Redis = await ConnectionMultiplexer.ConnectAsync(RedisConfiguration);
        _metricsProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
    }

    public AppDbContext CreateDbContext() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(DatabaseConnectionString, sql =>
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null))
                .Options,
            TimeProvider.System);

    public EmployeeService CreateEmployeeService(AppDbContext dbContext) =>
        new(dbContext, TimeProvider.System);

    public CreateEmployeeRequest CreateEmployeeRequest(
        string email,
        IReadOnlyCollection<CreateEmployeeActingUnitRequest>? actingUnits = null,
        IReadOnlyCollection<CreateEmployeeSectorRequest>? sectors = null) =>
        new(
            "Funcionário de integração",
            email,
            "+55 11 99999-0000",
            ProfessionGuid,
            PositionGuid,
            LevelGuid,
            HiringUnitGuid,
            new DateOnly(2026, 1, 1),
            actingUnits ?? [],
            sectors ?? []);

    public EmployeeOperationContext CreateOperationContext(Guid correlationId) =>
        new(Guid.NewGuid(), correlationId, Guid.NewGuid().ToString("N"), "127.0.0.1");

    public async Task DisposeAsync()
    {
        if (Redis is not null)
        {
            await Redis.CloseAsync();
            await Redis.DisposeAsync();
        }

        if (_metricsProvider is not null)
        {
            await _metricsProvider.DisposeAsync();
        }

        if (string.IsNullOrWhiteSpace(_masterConnectionString))
        {
            return;
        }

        ValidateDatabaseName(DatabaseName);
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{DatabaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{DatabaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedAsync(AppDbContext db)
    {
        var now = TimeProvider.System.GetUtcNow();
        var organization = new Organizacao(Guid.NewGuid(), $"Rede {IsolationKey}", now);
        db.Organizacoes.Add(organization);
        await db.SaveChangesAsync();

        var hiringUnit = new UnidadeHospitalar(Guid.NewGuid(), organization.Id, "Hospital B", now);
        var actingUnit = new UnidadeHospitalar(Guid.NewGuid(), organization.Id, "Hospital A", now);
        db.UnidadesHospitalares.AddRange(hiringUnit, actingUnit);
        await db.SaveChangesAsync();

        var profession = new Profissao(Guid.NewGuid(), $"Profissão {IsolationKey}", null, now);
        var position = new Cargo(Guid.NewGuid(), $"Cargo {IsolationKey}", null, now);
        var sector = new Setor(Guid.NewGuid(), actingUnit.Id, $"Farmácia {IsolationKey}", now);
        db.AddRange(profession, position, sector);
        await db.SaveChangesAsync();

        ProfessionGuid = profession.Guid;
        PositionGuid = position.Guid;
        HiringUnitGuid = hiringUnit.Guid;
        ActingUnitGuid = actingUnit.Guid;
        SectorGuid = sector.Guid;
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"A variável {name} é obrigatória para os testes reais.");

    private static void ValidateDatabaseName(string databaseName)
    {
        if (!TestDatabaseNamePattern().IsMatch(databaseName))
        {
            throw new InvalidOperationException("O nome do banco temporário não é seguro.");
        }
    }

    [GeneratedRegex("^sge_it_[a-f0-9]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex TestDatabaseNamePattern();
}
