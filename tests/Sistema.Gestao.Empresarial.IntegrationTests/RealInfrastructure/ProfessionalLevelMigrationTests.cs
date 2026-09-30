using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations;
using Sistema.Gestao.Empresarial.Infrastructure.ProfessionalCatalogs;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

/// <summary>
/// Valida, contra SQL Server real, a migration <c>NiveisProfissionaisConfiguraveis</c>:
/// compatibilidade com bancos existentes (JR/PL/SR preservados quando há
/// funcionários), instalação nova sem níveis obrigatórios, trava de duplicidade sob
/// a nova collation, provisionamento de permissões e rollback.
/// </summary>
[Collection(RealInfrastructureCollection.Name)]
public sealed partial class ProfessionalLevelMigrationTests(RealInfrastructureFixture fixture)
{
    private const string PreviousMigration = "20260910171319_GestaoSetoresHospitalares";
    private const string TargetMigration = "20260927163701_NiveisProfissionaisConfiguraveis";
    private const long LegacySeniorId = 3;

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task Upgrade_BancoEmUso_PreservaNiveisLegadosEConcedePermissoesAoAdministrador()
    {
        await using var scratch = await ScratchDatabase.CreateAsync(fixture, string.Empty);
        await using (var db = scratch.CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        }

        // Banco provisionado: funcionário no nível SR semeado e ADMINISTRADOR_INICIAL
        // com todas as permissões então existentes.
        int permissionsBefore;
        await using (var db = scratch.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            var organization = new Organizacao(Guid.NewGuid(), "Rede legada", now);
            db.Organizacoes.Add(organization);
            await db.SaveChangesAsync();
            // Este cenário usa schema histórico: insere somente colunas daquela migration.
            var unitGuid = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sge.UnidadesHospitalares (Guid, OrganizacaoId, Nome, Ativo, Excluido, DataCriacao, DataAtualizacao)
                VALUES ({unitGuid}, {organization.Id}, N'Hospital legado', 1, 0, {now}, {now});
                """);
            var unitId = await db.UnidadesHospitalares.Where(x => x.Guid == unitGuid).Select(x => x.Id).SingleAsync();
            var profession = new Profissao(Guid.NewGuid(), "Administração", null, now);
            var position = new Cargo(Guid.NewGuid(), "Administrador", null, now);
            var profile = new Perfil(Guid.NewGuid(), "ADMINISTRADOR_INICIAL", "Perfil de teste", now);
            db.AddRange(profession, position, profile);
            await db.SaveChangesAsync();
            // O modelo atual possui colunas posteriores a este schema historico.
            // Assim como a unidade acima, o funcionario deve usar somente as colunas da epoca.
            var employeeGuid = Guid.NewGuid();
            var admissionDate = new DateOnly(2025, 1, 1);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sge.Funcionarios
                    (Guid, Nome, Email, ProfissaoId, CargoId, NivelId, UnidadeContratacaoId,
                     DataAdmissao, Ativo, Excluido, DataCriacao, DataAtualizacao)
                VALUES
                    ({employeeGuid}, N'Administrador', N'admin-legado@hospital.test',
                     {profession.Id}, {position.Id}, {LegacySeniorId}, {unitId},
                     {admissionDate}, 1, 0, {now}, {now});
                """);
            var permissions = await db.Permissoes.AsNoTracking().ToListAsync();
            permissionsBefore = permissions.Count;
            db.PerfisPermissoes.AddRange(permissions.Select(p =>
                new PerfilPermissao(Guid.NewGuid(), profile.Id, p.Id, now)));
            await db.SaveChangesAsync();
        }

        await using (var db = scratch.CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(TargetMigration);
        }

        await using (var db = scratch.CreateContext())
        {
            // Os três níveis continuam ativos e o funcionário continua no seu nível.
            Assert.Equal(
                ["JR", "PL", "SR"],
                await db.NiveisProfissionais.OrderBy(x => x.Ordem).Select(x => x.Codigo).ToListAsync());
            Assert.Equal("SR", await db.Funcionarios.Select(x => x.Nivel.Codigo).SingleAsync());
            Assert.Equal(3, await db.NiveisProfissionais.IgnoreQueryFilters().CountAsync());
        }

        var granted = await ReadAdminPermissionCodesAsync(scratch);
        Assert.All(
            AdministratorProfilePermissionBackfill.ProfessionalLevelPermissionCodes,
            code => Assert.Contains(code, granted));
        Assert.Equal(permissionsBefore + 2, granted.Count);

        // Idempotência do backfill e do tratamento do seed legado.
        await ExecuteAsync(scratch,
            AdministratorProfilePermissionBackfill.GrantProfessionalLevelPermissionsToInitialAdministratorSql);
        await ExecuteAsync(scratch, LegacyProfessionalLevelSeed.RetireOnNewInstallationSql);
        Assert.Equal(granted.Count, (await ReadAdminPermissionCodesAsync(scratch)).Count);
        await using (var db = scratch.CreateContext())
        {
            Assert.Equal(3, await db.NiveisProfissionais.CountAsync());
        }
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task InstalacaoNova_CatalogoVazio_RollbackERecriacaoSemPerdaDeDados()
    {
        await using var scratch = await ScratchDatabase.CreateAsync(fixture, string.Empty);
        var defaultCollation = (string)(await ScalarAsync(scratch,
            "SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'))"))!;

        await using (var db = scratch.CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(TargetMigration);
        }

        Guid recreatedJuniorGuid;
        await using (var db = scratch.CreateContext())
        {
            // Nenhum nível disponível; os de exemplo existem fisicamente, excluídos
            // logicamente pela migration (sem ator).
            Assert.Empty(await db.NiveisProfissionais.ToListAsync());
            var legacy = await db.NiveisProfissionais.IgnoreQueryFilters().ToListAsync();
            Assert.Equal(3, legacy.Count);
            Assert.All(legacy, x =>
            {
                Assert.True(x.Excluido);
                Assert.False(x.Ativo);
                Assert.Null(x.ExcluidoPor);
                Assert.NotNull(x.ExcluidoEm);
            });

            // O gestor pode cadastrar "JR/Júnior" novamente (índices filtrados).
            var created = await new ProfessionalCatalogService(db, TimeProvider.System).CreateLevelAsync(
                new CreateProfessionalLevelRequest("JR", "Júnior", 1),
                new ProfessionalCatalogOperationContext(Guid.NewGuid(), Guid.NewGuid(), "migration-test", null),
                CancellationToken.None);
            recreatedJuniorGuid = created.Guid;
        }

        // Rollback: permissões removidas, índice de nome e collation revertidos, e os
        // níveis de exemplo reativados — exceto JR, cujo código já pertence ao nível
        // recriado pelo gestor.
        await using (var db = scratch.CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        }

        Assert.Equal(0, await ScalarAsync(scratch,
            "SELECT COUNT(*) FROM [sge].[Permissoes] WHERE [Codigo] LIKE 'NIVEL_PROFISSIONAL_[CE]%'"));
        Assert.Equal(0, await ScalarAsync(scratch,
            "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_NiveisProfissionais_Nome'"));
        Assert.Equal(1, await ScalarAsync(scratch,
            "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_NiveisProfissionais_Codigo' AND is_unique = 1"));
        Assert.Equal(defaultCollation, await ScalarAsync(scratch,
            "SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID('sge.NiveisProfissionais') AND name = 'Nome'"));
        Assert.Equal(
            "JR:0|JR:1|PL:0|SR:0",
            await ScalarAsync(scratch, """
                SELECT STRING_AGG(CONCAT([Codigo], ':', CONVERT(int, [Excluido])), '|')
                    WITHIN GROUP (ORDER BY [Codigo], [Excluido])
                FROM [sge].[NiveisProfissionais]
                """));

        // Reaplicar a migration funciona e não toca no nível cadastrado pelo gestor.
        await using (var db = scratch.CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(TargetMigration);
            Assert.Contains(TargetMigration, await db.Database.GetAppliedMigrationsAsync());
            var active = await db.NiveisProfissionais.ToListAsync();
            Assert.Equal(recreatedJuniorGuid, Assert.Single(active).Guid);
            Assert.Equal(4, await db.NiveisProfissionais.IgnoreQueryFilters().CountAsync());
        }
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task NomesQueColidemSobANovaCollation_DevemAbortarAMigrationSemAlterarDados()
    {
        // Collation sensível a caixa reproduz um banco que aceitava "Especialista" e
        // "ESPECIALISTA" como níveis distintos.
        await using var scratch = await ScratchDatabase.CreateAsync(fixture, "COLLATE Latin1_General_CS_AS");
        await using (var db = scratch.CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            var now = DateTimeOffset.UtcNow;
            db.NiveisProfissionais.AddRange(
                new NivelProfissional(Guid.NewGuid(), "ESP1", "Especialista", 4, now),
                new NivelProfissional(Guid.NewGuid(), "ESP2", "ESPECIALISTA", 5, now));
            await db.SaveChangesAsync();
        }

        Exception? error;
        await using (var db = scratch.CreateContext())
        {
            error = await Record.ExceptionAsync(() => db.Database.GetService<IMigrator>().MigrateAsync(TargetMigration));
        }

        Assert.NotNull(error);
        Assert.Equal(50001, FindSqlErrorNumber(error!));
        await using (var db = scratch.CreateContext())
        {
            Assert.DoesNotContain(TargetMigration, await db.Database.GetAppliedMigrationsAsync());
        }

        // Nada foi apagado nem alterado: os 3 níveis legados + os 2 conflitantes.
        Assert.Equal(5, await ScalarAsync(scratch,
            "SELECT COUNT(*) FROM [sge].[NiveisProfissionais] WHERE [Excluido] = 0"));
        Assert.Equal(1, await ScalarAsync(scratch,
            "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_NiveisProfissionais_Codigo'"));
        Assert.Equal(0, await ScalarAsync(scratch,
            "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_NiveisProfissionais_Nome'"));
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
