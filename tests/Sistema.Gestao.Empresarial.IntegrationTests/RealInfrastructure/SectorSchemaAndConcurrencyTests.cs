using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

/// <summary>
/// Cenários de setor que só o SQL Server real cobre: índices únicos filtrados,
/// collation case-insensitive e a corrida entre pré-checagem e índice único
/// (nome, sigla e vínculo ativo de unidade atendida).
/// </summary>
[Collection(RealInfrastructureCollection.Name)]
public sealed class SectorSchemaAndConcurrencyTests(RealInfrastructureFixture fixture)
{
    private const string ExplicitCollation = "Latin1_General_CI_AS";

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task Schema_DeveTerIndicesUnicosFiltradosECollationCaseInsensitive()
    {
        var (nomeUnique, nomeFilter) = await ReadIndexAsync("IX_Setores_UnidadeHospitalarId_Nome");
        var (siglaUnique, siglaFilter) = await ReadIndexAsync("IX_Setores_UnidadeHospitalarId_Sigla");

        Assert.True(nomeUnique);
        Assert.Contains("Excluido", nomeFilter);
        Assert.True(siglaUnique);
        Assert.Contains("Excluido", siglaFilter);

        Assert.Equal(ExplicitCollation, await ReadColumnCollationAsync("Setores", "Nome"));
        Assert.Equal(ExplicitCollation, await ReadColumnCollationAsync("Setores", "Sigla"));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task NomesConcorrentes_PersistemUmSetor_EClassificamFieldName()
    {
        var name = $"Setor nome concorrente {fixture.IsolationKey}";
        // Categorias distintas para que os dois creates não sejam serializados pelo
        // lock de categoria e a colisão ocorra no índice único (unidade, nome) —
        // exercitando a tradução de SQL 2601/2627.
        var (categoryA, categoryB) = (await SeedCategoryAsync(), await SeedCategoryAsync());

        var attempts = await Task.WhenAll(
            TryCreateSectorAsync(name, "CNA", categoryA),
            TryCreateSectorAsync(name, "CNB", categoryB));

        Assert.Single(attempts, x => x.Response is not null);
        var failure = Assert.Single(attempts, x => x.Error is not null).Error;
        var duplicate = Assert.IsType<DuplicateBusinessKeyException>(failure);
        Assert.Equal("name", duplicate.Field);

        await using var verification = fixture.CreateDbContext();
        Assert.Equal(1, await verification.Setores.CountAsync(x => x.Nome == name));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task SiglasConcorrentes_PersistemUmSetor_EClassificamFieldSigla()
    {
        var sigla = "CSG";
        var prefix = $"Setor sigla concorrente {fixture.IsolationKey}";
        var (categoryA, categoryB) = (await SeedCategoryAsync(), await SeedCategoryAsync());

        var attempts = await Task.WhenAll(
            TryCreateSectorAsync($"{prefix} A", sigla, categoryA),
            TryCreateSectorAsync($"{prefix} B", sigla, categoryB));

        Assert.Single(attempts, x => x.Response is not null);
        var failure = Assert.Single(attempts, x => x.Error is not null).Error;
        var duplicate = Assert.IsType<DuplicateBusinessKeyException>(failure);
        Assert.Equal("sigla", duplicate.Field);

        await using var verification = fixture.CreateDbContext();
        var unitId = await verification.UnidadesHospitalares
            .Where(u => u.Guid == fixture.HiringUnitGuid)
            .Select(u => u.Id)
            .SingleAsync();
        Assert.Equal(
            1,
            await verification.Setores.CountAsync(x =>
                x.UnidadeHospitalarId == unitId && x.Sigla == sigla));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task UnidadesAtendidasConcorrentes_PersistemUmVinculoAtivo_SemHttp500()
    {
        await using (var setup = fixture.CreateDbContext())
        {
            var request = NewSectorRequest(
                $"Setor compartilhado {fixture.IsolationKey}", "SHR", allowsSharedActing: true);
            var sector = await fixture.CreateOrganizationCatalogService(setup).CreateSectorAsync(
                request, Context(), CancellationToken.None);
            SharedSectorGuid = sector.Guid;
        }

        var startDate = new DateOnly(2026, 6, 1);
        var attempts = await Task.WhenAll(
            TryAddServedUnitAsync(SharedSectorGuid, fixture.ActingUnitGuid, startDate),
            TryAddServedUnitAsync(SharedSectorGuid, fixture.ActingUnitGuid, startDate));

        Assert.Single(attempts, x => x.Ok);
        var failure = Assert.Single(attempts, x => !x.Ok).Error;
        // Conflito controlado (422 via GlobalExceptionHandler), nunca SqlException
        // crua nem DbUpdateException vazando como HTTP 500.
        Assert.IsType<DomainException>(failure);

        await using var verification = fixture.CreateDbContext();
        var sectorId = await verification.Setores
            .Where(x => x.Guid == SharedSectorGuid)
            .Select(x => x.Id)
            .SingleAsync();
        Assert.Equal(
            1,
            await verification.SetoresUnidadesAtendidas.CountAsync(x => x.SetorId == sectorId && x.Ativo));
    }

    private Guid SharedSectorGuid { get; set; }

    private CreateSectorRequest NewSectorRequest(
        string name, string sigla, bool allowsSharedActing = false, Guid? categoryGuid = null) =>
        new(
            fixture.HiringUnitGuid,
            categoryGuid ?? fixture.SectorCategoryGuid,
            name,
            sigla,
            Description: null,
            InternalLocation: null,
            Extension: null,
            Email: null,
            ResponsibleEmployeeGuid: null,
            CareRelated: false,
            AllowsScheduleAllocation: false,
            AllowsSharedActing: allowsSharedActing,
            ServedUnits: null);

    private SectorOperationContext Context() =>
        new(fixture.ActorUserGuid, Guid.NewGuid(), Guid.NewGuid().ToString("N"), "127.0.0.1");

    private async Task<Guid> SeedCategoryAsync()
    {
        await using var db = fixture.CreateDbContext();
        var created = await fixture.CreateOrganizationCatalogService(db).CreateSectorCategoryAsync(
            new CreateSectorCategoryRequest($"Categoria índice {Guid.NewGuid():N}", null),
            Context(),
            CancellationToken.None);
        return created.Guid;
    }

    private async Task<SectorAttempt> TryCreateSectorAsync(string name, string sigla, Guid? categoryGuid = null)
    {
        try
        {
            await using var db = fixture.CreateDbContext();
            var response = await fixture.CreateOrganizationCatalogService(db).CreateSectorAsync(
                NewSectorRequest(name, sigla, categoryGuid: categoryGuid), Context(), CancellationToken.None);
            return new SectorAttempt(response, null);
        }
        // Único desfecho de falha esperado nesta corrida: a segunda criação com o
        // mesmo nome/sigla na mesma unidade. Qualquer outra exceção sobe e reprova
        // o teste.
        catch (DuplicateBusinessKeyException exception)
        {
            return new SectorAttempt(null, exception);
        }
    }

    private async Task<ServedUnitAttempt> TryAddServedUnitAsync(Guid sectorGuid, Guid unitGuid, DateOnly startDate)
    {
        try
        {
            await using var db = fixture.CreateDbContext();
            await fixture.CreateOrganizationCatalogService(db).AddSectorServedUnitAsync(
                sectorGuid,
                new AddSectorServedUnitRequest(unitGuid, startDate),
                Context(),
                CancellationToken.None);
            return new ServedUnitAttempt(true, null);
        }
        // Único desfecho de falha esperado: perder a corrida contra a desabilitação
        // da atuação compartilhada ou contra outro vínculo idêntico. Qualquer outra
        // exceção sobe e reprova o teste.
        catch (DomainException exception)
        {
            return new ServedUnitAttempt(false, exception);
        }
    }

    private async Task<(bool IsUnique, string Filter)> ReadIndexAsync(string indexName)
    {
        await using var connection = new SqlConnection(fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT is_unique, ISNULL(filter_definition, '') FROM sys.indexes WHERE name = @name";
        command.Parameters.AddWithValue("@name", indexName);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Índice {indexName} não encontrado.");
        return (reader.GetBoolean(0), reader.GetString(1));
    }

    private async Task<string?> ReadColumnCollationAsync(string table, string column)
    {
        await using var connection = new SqlConnection(fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID(@table) AND name = @column";
        command.Parameters.AddWithValue("@table", $"sge.{table}");
        command.Parameters.AddWithValue("@column", column);
        var value = await command.ExecuteScalarAsync();
        return value as string;
    }

    private sealed record SectorAttempt(SectorResponse? Response, Exception? Error);

    private sealed record ServedUnitAttempt(bool Ok, Exception? Error);
}
