using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Infrastructure.Organizations;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations;
using Sistema.Gestao.Empresarial.IntegrationTests.Organizations;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed class HospitalUnitDatabaseTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task UniqueIndexes_ResolveRacesAndRollBackAuditAndOutbox()
    {
        foreach (var field in new[] { "cnpj", "cnes", "internalCode" })
        {
            var gate = new SaveGate();
            var correlation = Guid.NewGuid();
            var request = HospitalUnitServiceTests.Request($"Race {field} {correlation}");
            request = field switch
            {
                "cnpj" => request with { Cnpj = "11222333000181" },
                "cnes" => request with { Cnes = "9134567" },
                _ => request with { InternalCode = correlation.ToString("N") }
            };
            async Task<Exception?> Attempt()
            {
                await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(fixture.DatabaseConnectionString).AddInterceptors(gate).Options, TimeProvider.System);
                return await Record.ExceptionAsync(() => new OrganizationCatalogService(db, TimeProvider.System)
                    .CreateHospitalUnitAsync(request, new(fixture.ActorUserGuid, correlation, "race", null), default));
            }
            var errors = await Task.WhenAll(Attempt(), Attempt());
            Assert.Single(errors, e => e is null);
            var conflict = Assert.IsType<DuplicateBusinessKeyException>(Assert.Single(errors, e => e is not null));
            Assert.Equal(field, conflict.Field);
            Assert.Contains(conflict.SqlErrorNumber, new int?[] { 2601, 2627 });
            await using var verification = fixture.CreateDbContext();
            Assert.Equal(1, await verification.UnidadesHospitalares.CountAsync(x => x.Nome == request.Name));
            Assert.Equal(1, await verification.AuditLogs.CountAsync(x => x.CorrelationId == correlation));
            Assert.Equal(1, await verification.OutboxMessages.CountAsync(x => x.CorrelationId == correlation));
        }
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task Upgrade_PreservesLegacyRowsAndForeignKeys_GrantsPermissionsIdempotently()
    {
        await using var scratch = await Scratch.CreateAsync(fixture.DatabaseConnectionString);
        var unitGuid = Guid.NewGuid();
        await using (var db = scratch.Context())
        {
            await db.GetService<IMigrator>().MigrateAsync("20260927163701_NiveisProfissionaisConfiguraveis");
            // Insere somente as colunas existentes à época. O modelo CLR atual já contém o novo cadastro.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sge.Organizacoes (Guid, Nome, Ativo, Excluido, DataCriacao, DataAtualizacao)
                VALUES (NEWID(), N'Rede legada', 1, 0, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                INSERT INTO sge.UnidadesHospitalares (Guid, OrganizacaoId, Nome, Ativo, Excluido, DataCriacao, DataAtualizacao)
                VALUES ({unitGuid}, SCOPE_IDENTITY(), N'Hospital legado', 1, 0, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                INSERT INTO sge.Perfis (Guid, Nome, Descricao, Ativo, Excluido, DataCriacao, DataAtualizacao)
                VALUES (NEWID(), N'ADMINISTRADOR_INICIAL', N'Teste', 1, 0, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                """);
            await db.Database.MigrateAsync();
            var legacy = await db.UnidadesHospitalares.SingleAsync();
            Assert.Equal(unitGuid, legacy.Guid);
            Assert.Equal("Hospital legado", legacy.Nome);
            Assert.Null(legacy.Cep);
            Assert.Null(legacy.Cnpj);
            var granted = await db.PerfisPermissoes.CountAsync(x => x.Permissao.Codigo.StartsWith("UNIDADE_HOSPITALAR_"));
            Assert.Equal(3, granted);
            await db.Database.ExecuteSqlRawAsync(AdministratorProfilePermissionBackfill.GrantHospitalUnitPermissionsSql);
            // Migrations posteriores também concedem permissões ao administrador: conta só as de unidades.
            Assert.Equal(3, await db.PerfisPermissoes.CountAsync(x => x.Permissao.Codigo.StartsWith("UNIDADE_HOSPITALAR_")));
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var sameName = new UnidadeHospitalar(Guid.NewGuid(), legacy.OrganizacaoId, "Hospital legado", DateTimeOffset.UtcNow);
            db.Add(sameName); await db.SaveChangesAsync();
            Assert.Equal(2, await db.UnidadesHospitalares.CountAsync());
            // Rollback antigo não pode apagar/consolidar nomes repetidos para restaurar o índice.
            await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>().MigrateAsync("20260927163701_NiveisProfissionaisConfiguraveis"));
            Assert.Equal(2, await db.UnidadesHospitalares.CountAsync());
        }
        await using var connection = new SqlConnection(scratch.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('sge.UnidadesHospitalares') AND is_unique=1 AND name IN ('IX_UnidadesHospitalares_Cnpj','IX_UnidadesHospitalares_Cnes','IX_UnidadesHospitalares_OrganizacaoId_CodigoInterno') AND filter_definition LIKE '%IS NOT NULL%'),
              (SELECT COUNT(*) FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID('sge.UnidadesHospitalares') AND delete_referential_action<>0),
              (SELECT COUNT(*) FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID('sge.UnidadesHospitalares'));
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(3, reader.GetInt32(0));
        Assert.Equal(0, reader.GetInt32(1));
        Assert.True(reader.GetInt32(2) >= 3);
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task SqlCrud_ProjectsSummaryAndPreservesRelationshipsOnDeactivation()
    {
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateOrganizationCatalogService(db);
        var context = new HospitalUnitOperationContext(fixture.ActorUserGuid, Guid.NewGuid(), "sql-crud", null);
        var created = await service.CreateHospitalUnitAsync(HospitalUnitServiceTests.Request("SQL Hospital"), context, default);
        var updated = await service.UpdateHospitalUnitAsync(created.Guid, HospitalUnitServiceTests.Request("SQL atualizado") with { TotalBeds = 8, IcuBeds = 2 }, context, default);
        Assert.Equal(8, updated!.TotalBeds);
        Assert.Single((await service.ListHospitalUnitsAsync(fixture.ActorUserGuid, new("SQL atualizado", true), default)).Items);
        var sectors = await db.Setores.CountAsync();
        var employees = await db.Funcionarios.CountAsync();
        var links = await db.FuncionariosUnidadesAtuacao.CountAsync();
        await service.ChangeHospitalUnitStatusAsync(fixture.HiringUnitGuid, false, context, default);
        Assert.Equal(sectors, await db.Setores.CountAsync());
        Assert.Equal(employees, await db.Funcionarios.CountAsync());
        Assert.Equal(links, await db.FuncionariosUnidadesAtuacao.CountAsync());
        await service.ChangeHospitalUnitStatusAsync(fixture.HiringUnitGuid, true, context, default);
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task OutboxFailure_RollsBackHospitalAndAudit()
    {
        var correlation = Guid.NewGuid();
        await using var db = fixture.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER [sge].[TR_HospitalTest_Outbox] ON [sge].[OutboxMessages] AFTER INSERT AS
            BEGIN THROW 51048, 'Outbox failure induced by test.', 1; END
            """);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.CreateOrganizationCatalogService(db).CreateHospitalUnitAsync(
                HospitalUnitServiceTests.Request(correlation.ToString()), new(fixture.ActorUserGuid, correlation, "rollback", null), default));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER [sge].[TR_HospitalTest_Outbox]"); }
        await using var verification = fixture.CreateDbContext();
        Assert.False(await verification.UnidadesHospitalares.AnyAsync(x => x.Nome == correlation.ToString()));
        Assert.False(await verification.AuditLogs.AnyAsync(x => x.CorrelationId == correlation));
        Assert.False(await verification.OutboxMessages.AnyAsync(x => x.CorrelationId == correlation));
    }

    private sealed class SaveGate : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }

    private sealed class Scratch(string name, string connectionString, string master) : IAsyncDisposable
    {
        public string ConnectionString => connectionString;
        public AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options, TimeProvider.System);
        public static async Task<Scratch> CreateAsync(string connectionString)
        {
            // Identificador gerado exclusivamente aqui; não contém entrada externa.
            var name = $"sge_it_{Guid.NewGuid():N}";
            var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;
            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
            return new(name, new SqlConnectionStringBuilder(connectionString) { InitialCatalog = name }.ConnectionString, master);
        }
        public async ValueTask DisposeAsync()
        {
            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }
    }
}
