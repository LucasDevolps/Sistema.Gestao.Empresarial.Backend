using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Organizations;
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
            new SectorListQuery(null, null, null, 1, 100),
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
            new SectorListQuery("Farmácia", true, fixture.PrimaryUnit.Guid, 1, 1),
            CancellationToken.None);

        Assert.Equal(1, units.Total);
        Assert.Single(units.Items);
        Assert.Equal(fixture.PrimaryUnit.Guid, units.Items.Single().Guid);
        Assert.Equal(1, sectors.Total);
        Assert.Single(sectors.Items);
        Assert.Equal(fixture.LocalSector.Guid, sectors.Items.Single().Guid);
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
    public UnidadeHospitalar ForeignUnit { get; private set; } = null!;
    public Setor LocalSector { get; private set; } = null!;
    public Setor ForeignSector { get; private set; } = null!;
    public Usuario Actor { get; private set; } = null!;

    public static async Task<OrganizationCatalogFixture> CreateAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, clock);
        var fixture = new OrganizationCatalogFixture(
            db, clock, new OrganizationCatalogService(db));
        await fixture.SeedAsync();
        return fixture;
    }

    private async Task SeedAsync()
    {
        var now = Clock.GetUtcNow();
        Organization = new Organizacao(Guid.NewGuid(), "Rede principal", now);
        var foreignOrganization = new Organizacao(Guid.NewGuid(), "Rede externa", now);
        Db.Organizacoes.AddRange(Organization, foreignOrganization);
        await Db.SaveChangesAsync();

        PrimaryUnit = new UnidadeHospitalar(Guid.NewGuid(), Organization.Id, "Hospital A", now);
        var secondaryUnit = new UnidadeHospitalar(Guid.NewGuid(), Organization.Id, "Hospital B", now);
        ForeignUnit = new UnidadeHospitalar(
            Guid.NewGuid(), foreignOrganization.Id, "Hospital externo", now);
        Db.UnidadesHospitalares.AddRange(PrimaryUnit, secondaryUnit, ForeignUnit);
        await Db.SaveChangesAsync();

        LocalSector = new Setor(Guid.NewGuid(), PrimaryUnit.Id, "Farmácia", now);
        ForeignSector = new Setor(Guid.NewGuid(), ForeignUnit.Id, "Farmácia externa", now);
        var profession = new Profissao(Guid.NewGuid(), "Profissão", null, now);
        var position = new Cargo(Guid.NewGuid(), "Cargo", null, now);
        var level = new NivelProfissional(Guid.NewGuid(), "TS", "Teste", 1, now);
        Db.AddRange(LocalSector, ForeignSector, profession, position, level);
        await Db.SaveChangesAsync();

        var employee = new Funcionario(
            Guid.NewGuid(), "Administrador", "admin@hospital.test", null,
            profession.Id, position.Id, level.Id, PrimaryUnit.Id,
            new DateOnly(2025, 1, 1), now);
        Db.Funcionarios.Add(employee);
        await Db.SaveChangesAsync();
        Actor = new Usuario(Guid.NewGuid(), employee.Id, employee.Email, "HASH_DE_TESTE", now);
        Db.Usuarios.Add(Actor);
        await Db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();
}
