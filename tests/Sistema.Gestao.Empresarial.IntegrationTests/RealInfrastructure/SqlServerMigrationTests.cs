using Microsoft.EntityFrameworkCore;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed class SqlServerMigrationTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task Migrations_DevemSerIdempotentesEDeixarSchemaAtualizado()
    {
        await using var db = fixture.CreateDbContext();

        await db.Database.MigrateAsync();
        var pending = await db.Database.GetPendingMigrationsAsync();
        var applied = await db.Database.GetAppliedMigrationsAsync();

        Assert.Empty(pending);
        Assert.Contains(applied, migration => migration.EndsWith("ProfessionalCatalogUseCases"));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task Schema_DeveConterSequenceEIndicesFiltradosObrigatorios()
    {
        await using var db = fixture.CreateDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM sys.sequences WHERE name = 'SequenciaMatriculaFuncionario' AND SCHEMA_NAME(schema_id) = 'sge'),
                (SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_Funcionarios_Email' AND is_unique = 1 AND filter_definition LIKE '%Excluido%'),
                (SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_FuncionariosUnidadesAtuacao_FuncionarioId_UnidadeHospitalarId' AND is_unique = 1 AND filter_definition LIKE '%Ativo%' AND filter_definition LIKE '%Excluido%'),
                (SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_FuncionariosSetores_FuncionarioId_SetorId' AND is_unique = 1 AND filter_definition LIKE '%Ativo%' AND filter_definition LIKE '%Excluido%'),
                (SELECT COUNT(*) FROM [sge].[NiveisProfissionais] WHERE [Codigo] IN ('JR', 'PL', 'SR') AND [Ativo] = 1),
                (SELECT COUNT(*) FROM [sge].[Permissoes] WHERE [Codigo] IN ('PROFISSAO_EDITAR', 'CARGO_VISUALIZAR', 'CARGO_CRIAR', 'CARGO_EDITAR', 'NIVEL_PROFISSIONAL_VISUALIZAR') AND [Ativo] = 1);
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(1, reader.GetInt32(1));
        Assert.Equal(1, reader.GetInt32(2));
        Assert.Equal(1, reader.GetInt32(3));
        Assert.Equal(3, reader.GetInt32(4));
        Assert.Equal(5, reader.GetInt32(5));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task Schema_ChavesDeNegocioDeCatalogo_DevemUsarCollationInsensivelACaixa()
    {
        await using var db = fixture.CreateDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT collation_name FROM sys.columns
                    WHERE object_id = OBJECT_ID('sge.Profissoes') AND name = 'Nome'),
                (SELECT collation_name FROM sys.columns
                    WHERE object_id = OBJECT_ID('sge.Cargos') AND name = 'Nome'),
                (SELECT COUNT(*) FROM sys.indexes
                    WHERE name = 'IX_Profissoes_Nome' AND is_unique = 1 AND filter_definition LIKE '%Excluido%'),
                (SELECT COUNT(*) FROM sys.indexes
                    WHERE name = 'IX_Cargos_Nome' AND is_unique = 1 AND filter_definition LIKE '%Excluido%');
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Latin1_General_CI_AS", reader.GetString(0));
        Assert.Equal("Latin1_General_CI_AS", reader.GetString(1));
        Assert.Equal(1, reader.GetInt32(2));
        Assert.Equal(1, reader.GetInt32(3));
    }
}
