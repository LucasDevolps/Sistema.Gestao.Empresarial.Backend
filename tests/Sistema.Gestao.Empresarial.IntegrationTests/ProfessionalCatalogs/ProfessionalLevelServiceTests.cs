using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Employees;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Infrastructure.ProfessionalCatalogs;
using EmployeeFixture = Sistema.Gestao.Empresarial.IntegrationTests.Employees.EmployeeFixture;

namespace Sistema.Gestao.Empresarial.IntegrationTests.ProfessionalCatalogs;

/// <summary>
/// CRUD de níveis profissionais (issue #47) no serviço de catálogos. A
/// insensibilidade a caixa do nome depende da collation do SQL Server e é coberta
/// em <c>RealInfrastructure/ProfessionalLevelConcurrencyTests</c>.
/// </summary>
public sealed class ProfessionalLevelServiceTests
{
    // ---------- Cadastro ----------

    [Fact]
    public async Task Criar_Valido_DevePersistirNormalizadoComAuditoriaEOutbox()
    {
        await using var fixture = CatalogFixture.Create();
        var correlation = Guid.NewGuid();

        var created = await fixture.Service.CreateLevelAsync(
            new CreateProfessionalLevelRequest("  esp ", "  Especialista  ", 4),
            fixture.Context(correlation),
            CancellationToken.None);

        Assert.Equal("ESP", created.Code);
        Assert.Equal("Especialista", created.Name);
        Assert.Equal(4, created.Order);
        Assert.True(created.Active);
        Assert.NotEqual(Guid.Empty, created.Guid);

        var audit = await fixture.Db.AuditLogs.SingleAsync(x => x.CorrelationId == correlation);
        Assert.Equal("NivelProfissional", audit.Entidade);
        Assert.Equal("CRIADO", audit.Acao);
        Assert.Equal(created.Guid, audit.EntidadeGuid);
        Assert.Null(audit.ValorAnterior);
        Assert.Single(await fixture.Db.OutboxMessages
            .Where(x => x.CorrelationId == correlation && x.EventType == "NivelProfissionalCriado")
            .ToListAsync());
    }

    [Fact]
    public async Task Criar_CodigoDuplicadoComVariacaoDeCaixaEEspacos_DeveRejeitarPeloCampoCode()
    {
        await using var fixture = CatalogFixture.Create();
        await CreateAsync(fixture, "JR", "Júnior", 1);

        var error = await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() =>
            CreateAsync(fixture, " jr ", "Outro nome", 2));

        Assert.Equal("code", error.Field);
        Assert.Equal("Já existe um nível profissional cadastrado com este código.", error.Message);
        Assert.Equal(1, await fixture.Db.NiveisProfissionais.CountAsync());
    }

    [Fact]
    public async Task Criar_NomeDuplicadoComEspacos_DeveRejeitarPeloCampoNameSemRepetirValor()
    {
        await using var fixture = CatalogFixture.Create();
        await CreateAsync(fixture, "SR", "Sênior", 3);

        var error = await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() =>
            CreateAsync(fixture, "SEN", "   Sênior   ", 3));

        Assert.Equal("name", error.Field);
        Assert.DoesNotContain("Sênior", error.Message);
        Assert.Equal(1, await fixture.Db.NiveisProfissionais.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_000)]
    public async Task Criar_OrdemForaDoIntervalo_DeveSerRejeitadaPeloDominioSemEfeitos(int order)
    {
        await using var fixture = CatalogFixture.Create();

        await Assert.ThrowsAsync<DomainException>(() => CreateAsync(fixture, "X", "Inválido", order));

        Assert.Empty(await fixture.Db.NiveisProfissionais.ToListAsync());
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());
        Assert.Empty(await fixture.Db.OutboxMessages.ToListAsync());
    }

    // ---------- Consulta ----------

    [Fact]
    public async Task Listar_DeveOrdenarPorOrdemENomeComOrdemRepetidaEPaginar()
    {
        await using var fixture = CatalogFixture.Create();
        await CreateAsync(fixture, "SR", "Sênior", 3);
        await CreateAsync(fixture, "PL", "Pleno", 2);
        await CreateAsync(fixture, "ESP", "Especialista", 3);
        await CreateAsync(fixture, "JR", "Júnior", 1);

        var firstPage = await fixture.Service.ListLevelsAsync(
            new ProfessionalCatalogListQuery(null, null, 1, 3), CancellationToken.None);
        var secondPage = await fixture.Service.ListLevelsAsync(
            new ProfessionalCatalogListQuery(null, null, 2, 3), CancellationToken.None);

        Assert.Equal(4, firstPage.Total);
        Assert.Equal(["JR", "PL", "ESP"], firstPage.Items.Select(x => x.Code));
        Assert.Equal(["SR"], secondPage.Items.Select(x => x.Code));
    }

    [Fact]
    public async Task Listar_BuscaDeveConsiderarCodigoENome()
    {
        await using var fixture = CatalogFixture.Create();
        await CreateAsync(fixture, "JR", "Júnior", 1);
        await CreateAsync(fixture, "COORD", "Coordenação", 5);

        var byCode = await fixture.Service.ListLevelsAsync(
            new ProfessionalCatalogListQuery("COORD", null), CancellationToken.None);
        var byName = await fixture.Service.ListLevelsAsync(
            new ProfessionalCatalogListQuery("Júni", true), CancellationToken.None);

        Assert.Equal(["COORD"], byCode.Items.Select(x => x.Code));
        Assert.Equal(["JR"], byName.Items.Select(x => x.Code));
    }

    [Fact]
    public async Task Consultar_ExistenteInexistenteEExcluido()
    {
        await using var fixture = CatalogFixture.Create();
        var level = await CreateAsync(fixture, "PL", "Pleno", 2);
        var deleted = await CreateAsync(fixture, "TMP", "Temporário", 9);
        await fixture.Service.DeleteLevelAsync(deleted.Guid, fixture.Context(Guid.NewGuid()), CancellationToken.None);

        var found = await fixture.Service.GetLevelAsync(level.Guid, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(level, found);
        Assert.Null(await fixture.Service.GetLevelAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Null(await fixture.Service.GetLevelAsync(deleted.Guid, CancellationToken.None));
    }

    // ---------- Edição ----------

    [Fact]
    public async Task Editar_Valido_DevePreservarIdentidadeEAuditarAntesEDepois()
    {
        await using var fixture = CatalogFixture.Create();
        var created = await CreateAsync(fixture, "SR", "Sênior", 3);
        var internalId = (await fixture.Db.NiveisProfissionais.SingleAsync()).Id;
        var correlation = Guid.NewGuid();

        var updated = await fixture.Service.UpdateLevelAsync(
            created.Guid,
            new UpdateProfessionalLevelRequest(" sr1 ", " Sênior I ", 4),
            fixture.Context(correlation),
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(created.Guid, updated.Guid);
        Assert.Equal("SR1", updated.Code);
        Assert.Equal("Sênior I", updated.Name);
        Assert.Equal(4, updated.Order);
        var stored = await fixture.Db.NiveisProfissionais.SingleAsync();
        Assert.Equal(internalId, stored.Id);

        var audit = await fixture.Db.AuditLogs.SingleAsync(x => x.CorrelationId == correlation);
        Assert.Equal("ATUALIZADO", audit.Acao);
        Assert.Contains("\"Codigo\":\"SR\"", audit.ValorAnterior!, StringComparison.Ordinal);
        Assert.Contains("\"Codigo\":\"SR1\"", audit.ValorNovo!, StringComparison.Ordinal);
        Assert.Single(await fixture.Db.OutboxMessages
            .Where(x => x.CorrelationId == correlation && x.EventType == "NivelProfissionalAtualizado")
            .ToListAsync());
    }

    [Fact]
    public async Task Editar_SemMudancas_DeveSerIdempotenteSemAuditoria()
    {
        await using var fixture = CatalogFixture.Create();
        var created = await CreateAsync(fixture, "PL", "Pleno", 2);
        var correlation = Guid.NewGuid();

        var updated = await fixture.Service.UpdateLevelAsync(
            created.Guid,
            new UpdateProfessionalLevelRequest("pl", " Pleno ", 2),
            fixture.Context(correlation),
            CancellationToken.None);

        Assert.Equal(created, updated);
        Assert.Empty(await fixture.Db.AuditLogs.Where(x => x.CorrelationId == correlation).ToListAsync());
        Assert.Empty(await fixture.Db.OutboxMessages.Where(x => x.CorrelationId == correlation).ToListAsync());
    }

    [Fact]
    public async Task Editar_Inexistente_DeveRetornarNull()
    {
        await using var fixture = CatalogFixture.Create();

        var updated = await fixture.Service.UpdateLevelAsync(
            Guid.NewGuid(),
            new UpdateProfessionalLevelRequest("X", "Qualquer", 1),
            fixture.Context(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(updated);
    }

    [Theory]
    [InlineData("jr", "Pleno", "code")]
    [InlineData("PL", "Júnior", "name")]
    public async Task Editar_ParaChaveDeOutroRegistro_DeveRejeitarSemAlterar(
        string code, string name, string expectedField)
    {
        await using var fixture = CatalogFixture.Create();
        await CreateAsync(fixture, "JR", "Júnior", 1);
        var pleno = await CreateAsync(fixture, "PL", "Pleno", 2);

        var error = await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() =>
            fixture.Service.UpdateLevelAsync(
                pleno.Guid,
                new UpdateProfessionalLevelRequest(code, name, 2),
                fixture.Context(Guid.NewGuid()),
                CancellationToken.None));

        Assert.Equal(expectedField, error.Field);
        Assert.Equal(pleno, await fixture.Service.GetLevelAsync(pleno.Guid, CancellationToken.None));
    }

    // ---------- Exclusão lógica ----------

    [Fact]
    public async Task Excluir_DeveMarcarExclusaoLogicaPreservandoRegistroEAuditando()
    {
        await using var fixture = CatalogFixture.Create();
        var level = await CreateAsync(fixture, "TMP", "Temporário", 9);
        var context = fixture.Context(Guid.NewGuid());

        var deleted = await fixture.Service.DeleteLevelAsync(level.Guid, context, CancellationToken.None);

        Assert.True(deleted);
        // Fisicamente presente, com os metadados de exclusão lógica.
        var stored = await fixture.Db.NiveisProfissionais.IgnoreQueryFilters()
            .SingleAsync(x => x.Guid == level.Guid);
        Assert.True(stored.Excluido);
        Assert.False(stored.Ativo);
        Assert.Equal(context.ActorUserGuid, stored.ExcluidoPor);
        Assert.Equal(fixture.Clock.GetUtcNow(), stored.ExcluidoEm);
        // Fora das consultas padrão.
        Assert.Empty((await fixture.Service.ListLevelsAsync(
            new ProfessionalCatalogListQuery(null, null), CancellationToken.None)).Items);
        Assert.Null(await fixture.Service.GetLevelAsync(level.Guid, CancellationToken.None));

        var audit = await fixture.Db.AuditLogs.SingleAsync(x => x.CorrelationId == context.CorrelationId);
        Assert.Equal("EXCLUIDO", audit.Acao);
        using var after = JsonDocument.Parse(audit.ValorNovo!);
        Assert.True(after.RootElement.GetProperty("Excluido").GetBoolean());
        Assert.Single(await fixture.Db.OutboxMessages
            .Where(x => x.CorrelationId == context.CorrelationId && x.EventType == "NivelProfissionalExcluido")
            .ToListAsync());
    }

    [Fact]
    public async Task Excluir_InexistenteOuJaExcluido_DeveRetornarFalseSemEfeitos()
    {
        await using var fixture = CatalogFixture.Create();
        var level = await CreateAsync(fixture, "TMP", "Temporário", 9);
        await fixture.Service.DeleteLevelAsync(level.Guid, fixture.Context(Guid.NewGuid()), CancellationToken.None);
        var auditCount = await fixture.Db.AuditLogs.CountAsync();

        Assert.False(await fixture.Service.DeleteLevelAsync(
            Guid.NewGuid(), fixture.Context(Guid.NewGuid()), CancellationToken.None));
        Assert.False(await fixture.Service.DeleteLevelAsync(
            level.Guid, fixture.Context(Guid.NewGuid()), CancellationToken.None));
        Assert.Null(await fixture.Service.UpdateLevelAsync(
            level.Guid,
            new UpdateProfessionalLevelRequest("TMP", "Temporário", 9),
            fixture.Context(Guid.NewGuid()),
            CancellationToken.None));
        Assert.Equal(auditCount, await fixture.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Excluir_LiberaCodigoENomeParaNovoCadastro()
    {
        await using var fixture = CatalogFixture.Create();
        var original = await CreateAsync(fixture, "JR", "Júnior", 1);
        await fixture.Service.DeleteLevelAsync(original.Guid, fixture.Context(Guid.NewGuid()), CancellationToken.None);

        var recreated = await CreateAsync(fixture, "JR", "Júnior", 1);

        Assert.NotEqual(original.Guid, recreated.Guid);
        Assert.Equal(2, await fixture.Db.NiveisProfissionais.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Excluir_NivelDeFuncionarioAtivo_DeveSerRejeitadoSemEfeitos()
    {
        await using var employees = await EmployeeFixture.CreateAsync();
        var service = new ProfessionalCatalogService(employees.Db, TimeProvider.System);
        var auditCount = await employees.Db.AuditLogs.CountAsync();
        var outboxCount = await employees.Db.OutboxMessages.CountAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => service.DeleteLevelAsync(
            employees.Level.Guid, CatalogContext(), CancellationToken.None));

        Assert.Equal("O nível profissional está vinculado a funcionários e não pode ser excluído.", error.Message);
        Assert.False(employees.Level.Excluido);
        Assert.NotNull(await service.GetLevelAsync(employees.Level.Guid, CancellationToken.None));
        Assert.Equal(auditCount, await employees.Db.AuditLogs.CountAsync());
        Assert.Equal(outboxCount, await employees.Db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Excluir_NivelDeFuncionarioInativo_TambemDeveSerRejeitadoParaPreservarHistorico()
    {
        await using var employees = await EmployeeFixture.CreateAsync();
        var service = new ProfessionalCatalogService(employees.Db, TimeProvider.System);
        var level = await service.CreateLevelAsync(
            new CreateProfessionalLevelRequest("PL", "Pleno", 2), CatalogContext(), CancellationToken.None);
        var employee = await employees.Service.CreateAsync(
            employees.CreateRequest(employees.UnitB.Guid, [], []) with { LevelGuid = level.Guid },
            employees.Context(Guid.NewGuid()),
            CancellationToken.None);
        await employees.Service.ChangeStatusAsync(
            employee.Guid, false, employees.Context(Guid.NewGuid()), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.DeleteLevelAsync(level.Guid, CatalogContext(), CancellationToken.None));

        // O funcionário inativo continua exibindo o próprio nível.
        var reloaded = await employees.Service.GetAsync(
            employee.Guid, employees.Context(Guid.NewGuid()), CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(level.Guid, reloaded.Level.Guid);
        Assert.Equal("PL", reloaded.Level.Code);
    }

    // ---------- Regressão: funcionário × nível excluído ----------

    [Fact]
    public async Task Funcionario_NaoPodeSerVinculadoANivelExcluido()
    {
        await using var employees = await EmployeeFixture.CreateAsync();
        var service = new ProfessionalCatalogService(employees.Db, TimeProvider.System);
        var level = await service.CreateLevelAsync(
            new CreateProfessionalLevelRequest("TMP", "Temporário", 9), CatalogContext(), CancellationToken.None);
        await service.DeleteLevelAsync(level.Guid, CatalogContext(), CancellationToken.None);

        var error = await Assert.ThrowsAsync<DomainException>(() => employees.Service.CreateAsync(
            employees.CreateRequest(employees.UnitB.Guid, [], []) with { LevelGuid = level.Guid },
            employees.Context(Guid.NewGuid()),
            CancellationToken.None));

        Assert.Equal("O nível profissional informado não existe ou está inativo.", error.Message);
    }

    [Fact]
    public async Task Funcionario_ListagemContinuaExibindoNivelAposEdicaoDoCatalogo()
    {
        await using var employees = await EmployeeFixture.CreateAsync();
        var service = new ProfessionalCatalogService(employees.Db, TimeProvider.System);
        await service.UpdateLevelAsync(
            employees.Level.Guid,
            new UpdateProfessionalLevelRequest("SR", "Sênior II", 3),
            CatalogContext(),
            CancellationToken.None);

        var page = await employees.Service.ListAsync(
            new EmployeeListQuery(null, null, null, 1, 50),
            employees.Context(Guid.NewGuid()),
            CancellationToken.None);

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item =>
        {
            Assert.Equal(employees.Level.Guid, item.Level.Guid);
            Assert.Equal("Sênior II", item.Level.Name);
        });
    }

    private static Task<ProfessionalLevelResponse> CreateAsync(
        CatalogFixture fixture, string code, string name, int order) =>
        fixture.Service.CreateLevelAsync(
            new CreateProfessionalLevelRequest(code, name, order),
            fixture.Context(Guid.NewGuid()),
            CancellationToken.None);

    private static ProfessionalCatalogOperationContext CatalogContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "catalog-test", "127.0.0.1");
}
