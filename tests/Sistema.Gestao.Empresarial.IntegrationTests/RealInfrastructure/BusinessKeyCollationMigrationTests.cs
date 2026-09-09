using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

/// <summary>
/// Exercita, contra SQL Server real, os dois pontos sensíveis da migration
/// <c>BusinessKeyCaseInsensitiveCollation</c>: a trava de segurança quando já existem
/// duplicatas sob a nova collation e a reversão (<c>Down</c>).
/// </summary>
[Collection(RealInfrastructureCollection.Name)]
public sealed partial class BusinessKeyCollationMigrationTests(RealInfrastructureFixture fixture)
{
    private const string PreviousMigration = "20260903145457_TemporaryUserLockout";
    private const string TargetMigration = "20260909152353_BusinessKeyCaseInsensitiveCollation";
    private const string ExplicitCollation = "Latin1_General_CI_AS";

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task MigrationComDuplicatasPreExistentes_DeveFalharSemAlterarDadosNemSchema()
    {
        // Collation case-sensitive: permite "Farmacêutico" e "FARMACÊUTICO" coexistirem
        // sob o índice único antigo, reproduzindo um banco anterior a esta PR.
        await using var scratch = await ScratchDatabase.CreateAsync(fixture, "COLLATE Latin1_General_CS_AS");

        await using (var db = scratch.CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            var now = DateTimeOffset.UtcNow;
            db.Profissoes.AddRange(
                new Profissao(Guid.NewGuid(), "Farmacêutico", null, now),
                new Profissao(Guid.NewGuid(), "FARMACÊUTICO", null, now));
            await db.SaveChangesAsync();
        }

        var (indiceAntesUnico, indiceAntesFiltro) = await ReadIndexAsync(scratch, "IX_Profissoes_Nome");

        Exception? erro;
        await using (var db = scratch.CreateContext())
        {
            erro = await Record.ExceptionAsync(() =>
                db.Database.GetService<IMigrator>().MigrateAsync(TargetMigration));
        }

        Assert.NotNull(erro);
        Assert.Equal(50001, FindSqlErrorNumber(erro!));

        await using (var db = scratch.CreateContext())
        {
            // Nada foi apagado nem consolidado: os dois registros continuam existindo.
            var nomes = (await db.Profissoes.AsNoTracking().Select(x => x.Nome).ToListAsync())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            Assert.Equal(["FARMACÊUTICO", "Farmacêutico"], nomes);
        }

        // Schema intacto: a migration não foi registrada e a coluna permanece sem a
        // collation explícita; o índice único filtrado antigo continua consistente.
        Assert.DoesNotContain(TargetMigration, await ReadAppliedMigrationsAsync(scratch));
        Assert.NotEqual(ExplicitCollation, await ReadColumnCollationAsync(scratch, "Profissoes", "Nome"));
        var (indiceDepoisUnico, indiceDepoisFiltro) = await ReadIndexAsync(scratch, "IX_Profissoes_Nome");
        Assert.True(indiceDepoisUnico);
        Assert.Equal(indiceAntesUnico, indiceDepoisUnico);
        Assert.Equal(indiceAntesFiltro, indiceDepoisFiltro);
        Assert.Contains("Excluido", indiceDepoisFiltro);
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task RollbackDaMigration_DeveRestaurarIndicesUnicosFiltradosSemPerderDados()
    {
        await using var scratch = await ScratchDatabase.CreateAsync(fixture, string.Empty);
        var defaultCollation = await ReadDatabaseCollationAsync(scratch);

        await using (var db = scratch.CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        Assert.Equal(ExplicitCollation, await ReadColumnCollationAsync(scratch, "Profissoes", "Nome"));
        Assert.Equal(ExplicitCollation, await ReadColumnCollationAsync(scratch, "Cargos", "Nome"));

        await using (var db = scratch.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            db.Profissoes.Add(new Profissao(Guid.NewGuid(), "Farmacêutico", null, now));
            db.Cargos.Add(new Cargo(Guid.NewGuid(), "Farmacêutico Clínico", null, now));
            await db.SaveChangesAsync();
        }

        await using (var db = scratch.CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        }

        // Índices únicos filtrados recriados pelo Down.
        foreach (var index in new[] { "IX_Profissoes_Nome", "IX_Cargos_Nome" })
        {
            var (unico, filtro) = await ReadIndexAsync(scratch, index);
            Assert.True(unico);
            Assert.Contains("Excluido", filtro);
        }

        // A collation explícita foi removida — a coluna volta a herdar a do banco.
        Assert.Equal(defaultCollation, await ReadColumnCollationAsync(scratch, "Profissoes", "Nome"));
        Assert.Equal(defaultCollation, await ReadColumnCollationAsync(scratch, "Cargos", "Nome"));

        // Sem perda de dados.
        await using (var db = scratch.CreateContext())
        {
            Assert.Equal(1, await db.Profissoes.CountAsync());
            Assert.Equal(1, await db.Cargos.CountAsync());
        }
    }

    private static async Task<IReadOnlyCollection<string>> ReadAppliedMigrationsAsync(ScratchDatabase scratch)
    {
        await using var db = scratch.CreateContext();
        return (await db.Database.GetAppliedMigrationsAsync()).ToList();
    }

    private static async Task<string?> ReadColumnCollationAsync(ScratchDatabase scratch, string table, string column)
    {
        var value = await ScalarAsync(scratch,
            $"SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID('sge.{table}') AND name = '{column}'");
        return value as string;
    }

    private static async Task<string> ReadDatabaseCollationAsync(ScratchDatabase scratch) =>
        (string)(await ScalarAsync(scratch, "SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'))"))!;

    private static async Task<(bool IsUnique, string Filter)> ReadIndexAsync(ScratchDatabase scratch, string indexName)
    {
        await using var connection = new SqlConnection(scratch.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT is_unique, ISNULL(filter_definition, '') FROM sys.indexes WHERE name = '{indexName}'";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Índice {indexName} não encontrado.");
        return (reader.GetBoolean(0), reader.GetString(1));
    }

    private static async Task<object?> ScalarAsync(ScratchDatabase scratch, string sql)
    {
        await using var connection = new SqlConnection(scratch.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return result == DBNull.Value ? null : result;
    }

    private static int? FindSqlErrorNumber(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                return sql.Number;
            }
        }

        return null;
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

        public static async Task<ScratchDatabase> CreateAsync(RealInfrastructureFixture fixture, string createSuffix)
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
                command.CommandText = $"CREATE DATABASE [{databaseName}] {createSuffix}";
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
