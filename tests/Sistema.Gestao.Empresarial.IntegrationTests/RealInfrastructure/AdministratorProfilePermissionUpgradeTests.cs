using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

/// <summary>
/// Valida, contra SQL Server real, que a migration da funcionalidade de setores
/// concede as novas permissões ao perfil <c>ADMINISTRADOR_INICIAL</c> já existente
/// em um banco previamente provisionado — sem duplicar, sem remover as anteriores e
/// de forma idempotente.
/// </summary>
[Collection(RealInfrastructureCollection.Name)]
public sealed partial class AdministratorProfilePermissionUpgradeTests(RealInfrastructureFixture fixture)
{
    private const string MigrationBeforeSectors = "20260909152353_BusinessKeyCaseInsensitiveCollation";

    private static readonly string[] NewSectorPermissionCodes =
    [
        "SETOR_CRIAR",
        "CATEGORIA_SETOR_VISUALIZAR",
        "CATEGORIA_SETOR_CRIAR",
        "CATEGORIA_SETOR_EDITAR",
    ];

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task MigrationDeSetores_ConcedeNovasPermissoesAoAdministradorInicialExistente_DeFormaIdempotente()
    {
        await using var scratch = await ScratchDatabase.CreateAsync(fixture);

        // 1. Banco provisionado até a migration anterior à de setores.
        await using (var db = scratch.CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(MigrationBeforeSectors);
        }

        // 2. ADMINISTRADOR_INICIAL já existe com TODAS as permissões então disponíveis.
        List<string> prAnteriores;
        await using (var db = scratch.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            var perfil = new Perfil(Guid.NewGuid(), "ADMINISTRADOR_INICIAL", "Perfil de teste", now);
            db.Perfis.Add(perfil);
            await db.SaveChangesAsync();

            var permissoes = await db.Permissoes.AsNoTracking().ToListAsync();
            prAnteriores = permissoes.Select(p => p.Codigo).OrderBy(x => x, StringComparer.Ordinal).ToList();
            db.PerfisPermissoes.AddRange(permissoes.Select(p =>
                new PerfilPermissao(Guid.NewGuid(), perfil.Id, p.Id, now)));
            await db.SaveChangesAsync();
        }

        Assert.DoesNotContain("SETOR_CRIAR", prAnteriores);

        // 3. Aplica a migration de setores (roda o backfill de permissões).
        await using (var db = scratch.CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        var depois = await ReadAdminPermissionCodesAsync(scratch);
        Assert.All(NewSectorPermissionCodes, code => Assert.Contains(code, depois));
        Assert.All(prAnteriores, code => Assert.Contains(code, depois));
        Assert.Equal(prAnteriores.Count + NewSectorPermissionCodes.Length, depois.Count);
        Assert.Equal(depois.Count, depois.Distinct().Count());

        // 4. Reexecutar o SQL do backfill não cria duplicidade (idempotência).
        await ExecuteAsync(
            scratch,
            AdministratorProfilePermissionBackfill.GrantSectorPermissionsToInitialAdministratorSql);
        var reexecutado = await ReadAdminPermissionCodesAsync(scratch);
        Assert.Equal(depois.Count, reexecutado.Count);
        Assert.Equal(reexecutado.Count, reexecutado.Distinct().Count());

        // 5. Reexecutar MigrateAsync() é no-op e mantém o estado.
        await using (var db = scratch.CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        var final = await ReadAdminPermissionCodesAsync(scratch);
        Assert.Equal(depois.Count, final.Count);
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task MigrationDeSetores_SemPerfilAdministrador_NaoCriaAssociacoes()
    {
        await using var scratch = await ScratchDatabase.CreateAsync(fixture);

        await using (var db = scratch.CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var verification = scratch.CreateContext();
        Assert.False(await verification.Perfis.AnyAsync(x => x.Nome == "ADMINISTRADOR_INICIAL"));
        Assert.False(await verification.PerfisPermissoes.AnyAsync());
    }

    private static async Task<List<string>> ReadAdminPermissionCodesAsync(ScratchDatabase scratch)
    {
        await using var db = scratch.CreateContext();
        var profileId = await db.Perfis
            .Where(x => x.Nome == "ADMINISTRADOR_INICIAL")
            .Select(x => x.Id)
            .SingleAsync();
        return await db.PerfisPermissoes
            .Where(pp => pp.PerfilId == profileId)
            .Select(pp => pp.Permissao.Codigo)
            .OrderBy(x => x)
            .ToListAsync();
    }

    private static async Task ExecuteAsync(ScratchDatabase scratch, string sql)
    {
        await using var connection = new SqlConnection(scratch.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed partial class ScratchDatabase : IAsyncDisposable
    {
        private readonly string _masterConnectionString;

        private ScratchDatabase(string databaseName, string connectionString, string masterConnectionString)
        {
            DatabaseName = databaseName;
            ConnectionString = connectionString;
            _masterConnectionString = masterConnectionString;
        }

        public string DatabaseName { get; }
        public string ConnectionString { get; }

        public static async Task<ScratchDatabase> CreateAsync(RealInfrastructureFixture fixture)
        {
            var databaseName = $"sge_it_{Guid.NewGuid():N}";
            if (!DatabaseNamePattern().IsMatch(databaseName))
            {
                throw new InvalidOperationException("Nome de banco temporário inseguro.");
            }

            var master = new SqlConnectionStringBuilder(fixture.DatabaseConnectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;
            var scratchConnectionString = new SqlConnectionStringBuilder(fixture.DatabaseConnectionString)
            {
                InitialCatalog = databaseName
            }.ConnectionString;

            await using (var connection = new SqlConnection(master))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{databaseName}]";
                await command.ExecuteNonQueryAsync();
            }

            return new ScratchDatabase(databaseName, scratchConnectionString, master);
        }

        public AppDbContext CreateContext() =>
            new(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(ConnectionString)
                    .Options,
                TimeProvider.System);

        public async ValueTask DisposeAsync()
        {
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

        [GeneratedRegex("^sge_it_[a-f0-9]{32}$", RegexOptions.CultureInvariant)]
        private static partial Regex DatabaseNamePattern();
    }
}
