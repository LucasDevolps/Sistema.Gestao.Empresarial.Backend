using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using Sistema.Gestao.Empresarial.Infrastructure.Organizations;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.IntegrationTests.Authentication;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Organizations;

public sealed class OrganizationCatalogServiceTests
{
    [Fact]
    public async Task Consultas_DeveRestringirUnidadesESetoresAOrganizacaoDoAtor()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();

        var organization = await fixture.Service.GetCurrentOrganizationAsync(
            fixture.Actor.Guid, CancellationToken.None);
        var units = await fixture.Service.ListHospitalUnitsAsync(
            fixture.Actor.Guid,
            new OrganizationCatalogListQuery(null, null, 1, 100),
            CancellationToken.None);
        var sectors = await fixture.Service.ListSectorsAsync(
            fixture.Actor.Guid,
            new SectorListQuery(null, null, null, null, 1, 100),
            CancellationToken.None);
        var foreignUnit = await fixture.Service.GetHospitalUnitAsync(
            fixture.Actor.Guid, fixture.ForeignUnit.Guid, CancellationToken.None);
        var foreignSector = await fixture.Service.GetSectorAsync(
            fixture.Actor.Guid, fixture.ForeignSector.Guid, CancellationToken.None);

        Assert.Equal(fixture.Organization.Guid, organization.Guid);
        Assert.Equal(2, units.Total);
        Assert.All(units.Items, x => Assert.Equal(fixture.Organization.Guid, x.Organization.Guid));
        Assert.Single(sectors.Items);
        Assert.Equal(fixture.LocalSector.Guid, sectors.Items.Single().Guid);
        Assert.Null(foreignUnit);
        Assert.Null(foreignSector);
    }

    [Fact]
    public async Task Listagens_DeveAplicarFiltrosEPaginacaoNoServidor()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();

        var units = await fixture.Service.ListHospitalUnitsAsync(
            fixture.Actor.Guid,
            new OrganizationCatalogListQuery("Hospital A", true, 1, 1),
            CancellationToken.None);
        var sectors = await fixture.Service.ListSectorsAsync(
            fixture.Actor.Guid,
            new SectorListQuery("Farmácia", true, fixture.PrimaryUnit.Guid, fixture.Category.Guid, 1, 1),
            CancellationToken.None);

        Assert.Equal(1, units.Total);
        Assert.Single(units.Items);
        Assert.Equal(fixture.PrimaryUnit.Guid, units.Items.Single().Guid);
        Assert.Equal(1, sectors.Total);
        Assert.Single(sectors.Items);
        var sector = sectors.Items.Single();
        Assert.Equal(fixture.LocalSector.Guid, sector.Guid);
        Assert.Equal(fixture.Category.Guid, sector.Category.Guid);
        Assert.False(string.IsNullOrWhiteSpace(sector.Sigla));
    }

    [Fact]
    public async Task AtorSemEscopoOrganizacionalAtivo_DeveSerNegado()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        fixture.Actor.Inativar(fixture.Clock.GetUtcNow());
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<OrganizationAccessDeniedException>(() =>
            fixture.Service.ListHospitalUnitsAsync(
                fixture.Actor.Guid,
                new OrganizationCatalogListQuery(null, true),
                CancellationToken.None));
    }

    [Fact]
    public async Task CriarSetor_DeveNormalizarSiglaEPersistirAuditoriaEOutbox()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var correlationId = Guid.NewGuid();

        var created = await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("Central de Análise de Prescrições", "  cap "),
            fixture.Context(correlationId),
            CancellationToken.None);

        Assert.Equal("CAP", created.Sigla);
        Assert.Equal("Central de Análise de Prescrições", created.Name);
        Assert.Equal(fixture.Category.Guid, created.Category.Guid);
        Assert.True(created.Active);
        Assert.Single(await fixture.Db.AuditLogs
            .Where(x => x.CorrelationId == correlationId && x.Entidade == "Setor").ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages
            .Where(x => x.CorrelationId == correlationId && x.EventType == "SetorCriado").ToListAsync());
    }

    [Fact]
    public async Task CriarSetor_ComNomeOuSiglaDuplicadosNaMesmaUnidade_DeveRejeitar()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("Beira Leito", "BL"),
            fixture.Context(),
            CancellationToken.None);

        var nameConflict = await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() =>
            fixture.Service.CreateSectorAsync(
                fixture.NewSectorRequest("  Beira Leito  ", "BL2"),
                fixture.Context(),
                CancellationToken.None));
        var siglaConflict = await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() =>
            fixture.Service.CreateSectorAsync(
                fixture.NewSectorRequest("Beira Leito Norte", " bl "),
                fixture.Context(),
                CancellationToken.None));

        Assert.Equal("name", nameConflict.Field);
        Assert.Equal("sigla", siglaConflict.Field);
        Assert.Equal(2, await fixture.Db.Setores.CountAsync(x => x.UnidadeHospitalarId == fixture.PrimaryUnit.Id));
    }

    [Fact]
    public async Task CriarSetor_MesmaSiglaEmOutraUnidadeDaOrganizacao_DevePermitir()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("UTI Adulto", "UTI", unitGuid: fixture.PrimaryUnit.Guid),
            fixture.Context(),
            CancellationToken.None);

        var second = await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("UTI Adulto", "UTI", unitGuid: fixture.SecondaryUnit.Guid),
            fixture.Context(),
            CancellationToken.None);

        Assert.Equal("UTI", second.Sigla);
        Assert.Equal(fixture.SecondaryUnit.Guid, second.Unit.Guid);
    }

    [Fact]
    public async Task CriarSetor_ComUnidadeDeOutraOrganizacao_DeveRejeitar()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.CreateSectorAsync(
                fixture.NewSectorRequest("Setor externo", "EXT", unitGuid: fixture.ForeignUnit.Guid),
                fixture.Context(),
                CancellationToken.None));
        Assert.Equal(0, await fixture.Db.Setores.CountAsync(x => x.Sigla == "EXT"));
    }

    [Fact]
    public async Task CriarSetor_ComUnidadesAtendidasSemAtuacaoCompartilhada_DeveRejeitar()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var request = fixture.NewSectorRequest("CAP", "CAP") with
        {
            AllowsSharedActing = false,
            ServedUnits = [new CreateSectorServedUnitRequest(fixture.SecondaryUnit.Guid, new DateOnly(2026, 1, 1))]
        };

        await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.CreateSectorAsync(request, fixture.Context(), CancellationToken.None));
    }

    [Fact]
    public async Task CriarSetor_ComAtuacaoCompartilhada_DevePersistirUnidadesAtendidas()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var request = fixture.NewSectorRequest("CAP", "CAP") with
        {
            AllowsSharedActing = true,
            ServedUnits = [new CreateSectorServedUnitRequest(fixture.SecondaryUnit.Guid, new DateOnly(2026, 1, 2))]
        };

        var created = await fixture.Service.CreateSectorAsync(
            request, fixture.Context(), CancellationToken.None);

        Assert.True(created.AllowsSharedActing);
        var served = Assert.Single(created.ServedUnits);
        Assert.Equal(fixture.SecondaryUnit.Guid, served.UnitGuid);
        Assert.True(served.Active);
    }

    [Fact]
    public async Task AtualizarSetor_SemMudanca_NaoEmiteEventoENaoAlteraDataAtualizacao()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var created = await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("Pronto-Socorro", "PS"),
            fixture.Context(),
            CancellationToken.None);
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));

        var updated = await fixture.Service.UpdateSectorAsync(
            created.Guid,
            fixture.UpdateRequestFrom(created),
            fixture.Context(),
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(created.UpdatedAt, updated!.UpdatedAt);
        Assert.Empty(await fixture.Db.OutboxMessages
            .Where(x => x.EventType == "SetorAtualizado").ToListAsync());
    }

    [Fact]
    public async Task AtualizarSetor_DesabilitarCompartilhamentoComUnidadeAtendidaAtiva_DeveRejeitar()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var request = fixture.NewSectorRequest("CAP", "CAP") with
        {
            AllowsSharedActing = true,
            ServedUnits = [new CreateSectorServedUnitRequest(fixture.SecondaryUnit.Guid, new DateOnly(2026, 1, 2))]
        };
        var created = await fixture.Service.CreateSectorAsync(request, fixture.Context(), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.UpdateSectorAsync(
                created.Guid,
                fixture.UpdateRequestFrom(created) with { AllowsSharedActing = false },
                fixture.Context(),
                CancellationToken.None));
    }

    [Fact]
    public async Task InativarEReativarSetor_DeveRefletirStatusEEmitirEventos()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var created = await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("Centro Cirúrgico", "CC"),
            fixture.Context(),
            CancellationToken.None);

        var inactivated = await fixture.Service.ChangeSectorStatusAsync(
            created.Guid, false, fixture.Context(), CancellationToken.None);
        var reactivated = await fixture.Service.ChangeSectorStatusAsync(
            created.Guid, true, fixture.Context(), CancellationToken.None);
        var unchanged = await fixture.Service.ChangeSectorStatusAsync(
            created.Guid, true, fixture.Context(), CancellationToken.None);

        Assert.False(inactivated!.Active);
        Assert.True(reactivated!.Active);
        Assert.True(unchanged!.Active);
        Assert.Single(await fixture.Db.OutboxMessages.Where(x => x.EventType == "SetorInativado").ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages.Where(x => x.EventType == "SetorReativado").ToListAsync());
    }

    [Fact]
    public async Task ReativarSetor_ComCategoriaInativa_DeveRecusarSemEfeitos()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var category = await fixture.Service.CreateSectorCategoryAsync(
            new CreateSectorCategoryRequest("Emergência", null), fixture.Context(), CancellationToken.None);
        var sector = await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("Pronto-Socorro Central", "PSC") with { CategoryGuid = category.Guid },
            fixture.Context(),
            CancellationToken.None);
        await fixture.Service.ChangeSectorStatusAsync(
            sector.Guid, false, fixture.Context(), CancellationToken.None);
        await fixture.Service.ChangeSectorCategoryStatusAsync(
            category.Guid, false, fixture.Context(), CancellationToken.None);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.ChangeSectorStatusAsync(
                sector.Guid, true, fixture.Context(), CancellationToken.None));

        Assert.Equal(
            "A categoria associada ao setor está inativa e impede sua reativação.", error.Message);
        var detail = await fixture.Service.GetSectorAsync(
            fixture.Actor.Guid, sector.Guid, CancellationToken.None);
        Assert.False(detail!.Active);
        Assert.Empty(await fixture.Db.OutboxMessages
            .Where(x => x.EventType == "SetorReativado").ToListAsync());
        Assert.Empty(await fixture.Db.AuditLogs
            .Where(x => x.Entidade == "Setor" && x.Acao == "REATIVADO").ToListAsync());
    }

    [Fact]
    public async Task ReativarSetor_ComCategoriaAtiva_DeveFuncionarNormalmente()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var category = await fixture.Service.CreateSectorCategoryAsync(
            new CreateSectorCategoryRequest("Cirúrgico", null), fixture.Context(), CancellationToken.None);
        var sector = await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("Bloco Cirúrgico 2", "BC2") with { CategoryGuid = category.Guid },
            fixture.Context(),
            CancellationToken.None);
        await fixture.Service.ChangeSectorStatusAsync(
            sector.Guid, false, fixture.Context(), CancellationToken.None);

        var reactivated = await fixture.Service.ChangeSectorStatusAsync(
            sector.Guid, true, fixture.Context(), CancellationToken.None);

        Assert.True(reactivated!.Active);
        Assert.Single(await fixture.Db.OutboxMessages
            .Where(x => x.EventType == "SetorReativado").ToListAsync());
    }

    [Fact]
    public async Task AdicionarEEncerrarUnidadeAtendida_DevePreservarHistorico()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var created = await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("CAP", "CAP") with { AllowsSharedActing = true },
            fixture.Context(),
            CancellationToken.None);

        var added = await fixture.Service.AddSectorServedUnitAsync(
            created.Guid,
            new AddSectorServedUnitRequest(fixture.SecondaryUnit.Guid, new DateOnly(2026, 1, 5)),
            fixture.Context(),
            CancellationToken.None);
        var ended = await fixture.Service.EndSectorServedUnitAsync(
            created.Guid, added!.Guid, new DateOnly(2026, 2, 1), fixture.Context(), CancellationToken.None);

        Assert.True(ended);
        var detail = await fixture.Service.GetSectorAsync(
            fixture.Actor.Guid, created.Guid, CancellationToken.None);
        var served = Assert.Single(detail!.ServedUnits);
        Assert.False(served.Active);
        Assert.Equal(new DateOnly(2026, 2, 1), served.EndDate);
        Assert.Equal(1, await fixture.Db.SetoresUnidadesAtendidas.CountAsync());
    }

    [Fact]
    public async Task CriarCategoria_DuplicadaExata_DeveRejeitarEInativarEmUso_TambemRejeita()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();

        await fixture.Service.CreateSectorCategoryAsync(
            new CreateSectorCategoryRequest("Diagnóstico", null),
            fixture.Context(),
            CancellationToken.None);
        await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() =>
            fixture.Service.CreateSectorCategoryAsync(
                new CreateSectorCategoryRequest("  Diagnóstico ", null),
                fixture.Context(),
                CancellationToken.None));

        await fixture.Service.CreateSectorAsync(
            fixture.NewSectorRequest("Laboratório", "LAB"),
            fixture.Context(),
            CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.ChangeSectorCategoryStatusAsync(
                fixture.Category.Guid, false, fixture.Context(), CancellationToken.None));
    }

    [Fact]
    public async Task GetSector_DeveTrazerResponsavelECamposDeContato()
    {
        await using var fixture = await OrganizationCatalogFixture.CreateAsync();
        var request = fixture.NewSectorRequest("Farmácia Central", "FC") with
        {
            Description = "Análise clínica de prescrições",
            InternalLocation = "Bloco A",
            Extension = "2100",
            Email = "FARMACIA@HOSPITAL.TEST",
            ResponsibleEmployeeGuid = fixture.ActorEmployee.Guid,
            CareRelated = true,
            AllowsScheduleAllocation = true
        };
        var created = await fixture.Service.CreateSectorAsync(request, fixture.Context(), CancellationToken.None);

        var detail = await fixture.Service.GetSectorAsync(
            fixture.Actor.Guid, created.Guid, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("farmacia@hospital.test", detail!.Email);
        Assert.Equal("Bloco A", detail.InternalLocation);
        Assert.Equal("2100", detail.Extension);
        Assert.True(detail.CareRelated);
        Assert.True(detail.AllowsScheduleAllocation);
        Assert.NotNull(detail.Responsible);
        Assert.Equal(fixture.ActorEmployee.Guid, detail.Responsible!.Guid);
    }
}

internal sealed class OrganizationCatalogFixture : IAsyncDisposable
{
    private OrganizationCatalogFixture(
        AppDbContext db,
        ManualTimeProvider clock,
        OrganizationCatalogService service)
    {
        Db = db;
        Clock = clock;
        Service = service;
    }

    public AppDbContext Db { get; }
    public ManualTimeProvider Clock { get; }
    public OrganizationCatalogService Service { get; }
    public Organizacao Organization { get; private set; } = null!;
    public UnidadeHospitalar PrimaryUnit { get; private set; } = null!;
    public UnidadeHospitalar SecondaryUnit { get; private set; } = null!;
    public UnidadeHospitalar ForeignUnit { get; private set; } = null!;
    public CategoriaSetor Category { get; private set; } = null!;
    public Setor LocalSector { get; private set; } = null!;
    public Setor ForeignSector { get; private set; } = null!;
    public Funcionario ActorEmployee { get; private set; } = null!;
    public Usuario Actor { get; private set; } = null!;

    public static async Task<OrganizationCatalogFixture> CreateAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, clock);
        var fixture = new OrganizationCatalogFixture(
            db, clock, new OrganizationCatalogService(db, clock));
        await fixture.SeedAsync();
        return fixture;
    }

    public SectorOperationContext Context(Guid? correlationId = null) =>
        new(Actor.Guid, correlationId ?? Guid.NewGuid(), "org-test", "127.0.0.1");

    public CreateSectorRequest NewSectorRequest(string name, string sigla, Guid? unitGuid = null) =>
        new(
            unitGuid ?? PrimaryUnit.Guid,
            Category.Guid,
            name,
            sigla,
            Description: null,
            InternalLocation: null,
            Extension: null,
            Email: null,
            ResponsibleEmployeeGuid: null,
            CareRelated: false,
            AllowsScheduleAllocation: false,
            AllowsSharedActing: false,
            ServedUnits: null);

    public UpdateSectorRequest UpdateRequestFrom(SectorResponse sector) =>
        new(
            sector.Category.Guid,
            sector.Name,
            sector.Sigla,
            sector.Description,
            sector.InternalLocation,
            sector.Extension,
            sector.Email,
            sector.Responsible?.Guid,
            sector.CareRelated,
            sector.AllowsScheduleAllocation,
            sector.AllowsSharedActing);

    private async Task SeedAsync()
    {
        var now = Clock.GetUtcNow();
        Organization = new Organizacao(Guid.NewGuid(), "Rede principal", now);
        var foreignOrganization = new Organizacao(Guid.NewGuid(), "Rede externa", now);
        Db.Organizacoes.AddRange(Organization, foreignOrganization);
        await Db.SaveChangesAsync();

        PrimaryUnit = new UnidadeHospitalar(Guid.NewGuid(), Organization.Id, "Hospital A", now);
        SecondaryUnit = new UnidadeHospitalar(Guid.NewGuid(), Organization.Id, "Hospital B", now);
        ForeignUnit = new UnidadeHospitalar(
            Guid.NewGuid(), foreignOrganization.Id, "Hospital externo", now);
        Db.UnidadesHospitalares.AddRange(PrimaryUnit, SecondaryUnit, ForeignUnit);
        await Db.SaveChangesAsync();

        Category = new CategoriaSetor(Guid.NewGuid(), "Assistencial", null, now);
        Db.CategoriasSetores.Add(Category);
        await Db.SaveChangesAsync();

        LocalSector = new Setor(
            Guid.NewGuid(), PrimaryUnit.Id, Category.Id, "Farmácia", "FARM",
            null, null, null, null, null, false, false, false, now);
        ForeignSector = new Setor(
            Guid.NewGuid(), ForeignUnit.Id, Category.Id, "Farmácia externa", "FARMX",
            null, null, null, null, null, false, false, false, now);
        var profession = new Profissao(Guid.NewGuid(), "Profissão", null, now);
        var position = new Cargo(Guid.NewGuid(), "Cargo", null, now);
        var level = new NivelProfissional(Guid.NewGuid(), "TS", "Teste", 1, now);
        Db.AddRange(LocalSector, ForeignSector, profession, position, level);
        await Db.SaveChangesAsync();

        ActorEmployee = new Funcionario(
            Guid.NewGuid(), "Administrador", "admin@hospital.test", null,
            profession.Id, position.Id, level.Id, PrimaryUnit.Id,
            new DateOnly(2025, 1, 1), now);
        Db.Funcionarios.Add(ActorEmployee);
        await Db.SaveChangesAsync();
        Actor = new Usuario(Guid.NewGuid(), ActorEmployee.Id, ActorEmployee.Email, "HASH_DE_TESTE", now);
        Db.Usuarios.Add(Actor);
        await Db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();
}
