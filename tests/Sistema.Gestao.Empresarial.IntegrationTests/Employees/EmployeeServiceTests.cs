using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Employees;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Employees;

public sealed class EmployeeServiceTests
{
    [Fact]
    public async Task Criar_DeveSepararUnidadeDeContratacaoDasUnidadesDeAtuacao()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var correlationId = Guid.NewGuid();

        var employee = await fixture.Service.CreateAsync(
            fixture.CreateRequest(
                fixture.UnitB.Guid,
                [new CreateEmployeeActingUnitRequest(fixture.UnitA.Guid, new DateOnly(2026, 1, 10))],
                [new CreateEmployeeSectorRequest(fixture.SectorA.Guid, new DateOnly(2026, 1, 15))]),
            fixture.Context(correlationId),
            CancellationToken.None);

        Assert.Equal(fixture.UnitB.Guid, employee.HiringUnit.Guid);
        Assert.DoesNotContain(employee.ActingUnits, x => x.UnitGuid == fixture.UnitB.Guid);
        Assert.Contains(employee.ActingUnits, x => x.UnitGuid == fixture.UnitA.Guid && x.Active);
        Assert.Contains(employee.Sectors, x => x.SectorGuid == fixture.SectorA.Guid && x.Active);
        Assert.Single(await fixture.Db.AuditLogs.Where(x => x.CorrelationId == correlationId).ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages.Where(x => x.CorrelationId == correlationId).ToListAsync());
    }

    [Fact]
    public async Task Criar_DeveRejeitarAtuacaoEmOutraOrganizacaoSemPersistirEfeitos()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var request = fixture.CreateRequest(
            fixture.UnitB.Guid,
            [new CreateEmployeeActingUnitRequest(fixture.OtherOrganizationUnit.Guid, new DateOnly(2026, 1, 10))],
            []);

        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(
            request, fixture.Context(Guid.NewGuid()), CancellationToken.None));

        Assert.Single(await fixture.Db.Funcionarios.ToListAsync());
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());
        Assert.Empty(await fixture.Db.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task EncerrarVinculos_DeveExigirOrdemConsistenteEPreservarHistoricoIdempotente()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var employee = await fixture.Service.CreateAsync(
            fixture.CreateRequest(
                fixture.UnitB.Guid,
                [new CreateEmployeeActingUnitRequest(fixture.UnitA.Guid, new DateOnly(2026, 1, 10))],
                [new CreateEmployeeSectorRequest(fixture.SectorA.Guid, new DateOnly(2026, 1, 15))]),
            fixture.Context(Guid.NewGuid()),
            CancellationToken.None);
        var actingUnit = Assert.Single(employee.ActingUnits);
        var sector = Assert.Single(employee.Sectors);

        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.EndActingUnitAsync(
            employee.Guid, actingUnit.Guid, new DateOnly(2026, 8, 1),
            fixture.Context(Guid.NewGuid()), CancellationToken.None));

        var sectorCorrelationId = Guid.NewGuid();
        Assert.True(await fixture.Service.EndSectorAsync(
            employee.Guid, sector.Guid, new DateOnly(2026, 8, 1),
            fixture.Context(sectorCorrelationId), CancellationToken.None));
        Assert.False(await fixture.Service.EndSectorAsync(
            employee.Guid, sector.Guid, new DateOnly(2026, 8, 1),
            fixture.Context(Guid.NewGuid()), CancellationToken.None));

        var actingCorrelationId = Guid.NewGuid();
        Assert.True(await fixture.Service.EndActingUnitAsync(
            employee.Guid, actingUnit.Guid, new DateOnly(2026, 8, 2),
            fixture.Context(actingCorrelationId), CancellationToken.None));
        Assert.False(await fixture.Service.EndActingUnitAsync(
            employee.Guid, actingUnit.Guid, new DateOnly(2026, 8, 2),
            fixture.Context(Guid.NewGuid()), CancellationToken.None));

        fixture.Db.ChangeTracker.Clear();
        var storedSector = await fixture.Db.FuncionariosSetores.SingleAsync(x => x.Guid == sector.Guid);
        var storedActingUnit = await fixture.Db.FuncionariosUnidadesAtuacao.SingleAsync(x => x.Guid == actingUnit.Guid);
        Assert.False(storedSector.Ativo);
        Assert.False(storedSector.Excluido);
        Assert.Equal(new DateOnly(2026, 8, 1), storedSector.DataFim);
        Assert.False(storedActingUnit.Ativo);
        Assert.False(storedActingUnit.Excluido);
        Assert.Equal(new DateOnly(2026, 8, 2), storedActingUnit.DataFim);
        Assert.Single(await fixture.Db.OutboxMessages.Where(x => x.CorrelationId == sectorCorrelationId).ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages.Where(x => x.CorrelationId == actingCorrelationId).ToListAsync());
    }

    [Fact]
    public async Task AlterarStatusRepetidamente_NaoDeveDuplicarAuditoriaNemEvento()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var employee = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.UnitB.Guid, [], []),
            fixture.Context(Guid.NewGuid()),
            CancellationToken.None);
        var correlationId = Guid.NewGuid();

        var first = await fixture.Service.ChangeStatusAsync(
            employee.Guid, false, fixture.Context(correlationId), CancellationToken.None);
        var second = await fixture.Service.ChangeStatusAsync(
            employee.Guid, false, fixture.Context(Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.False(first.Active);
        Assert.False(second.Active);
        Assert.Single(await fixture.Db.AuditLogs.Where(x => x.Acao == "INATIVADO").ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages.Where(x => x.EventType == "FuncionarioInativado").ToListAsync());
    }

    [Fact]
    public async Task ConsultasEMutacoes_DevemOcultarFuncionarioDeOutraOrganizacao()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var external = new Funcionario(
            Guid.NewGuid(), "Funcionário externo", $"externo-{Guid.NewGuid():N}@hospital.test", null,
            fixture.Profession.Id, fixture.Position.Id, fixture.Level.Id,
            fixture.OtherOrganizationUnit.Id, new DateOnly(2026, 1, 1),
            new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));
        fixture.Db.Funcionarios.Add(external);
        await fixture.Db.SaveChangesAsync();

        var context = fixture.Context(Guid.NewGuid());
        var page = await fixture.Service.ListAsync(
            new EmployeeListQuery(null, null, null), context, CancellationToken.None);
        var found = await fixture.Service.GetAsync(external.Guid, context, CancellationToken.None);
        var updated = await fixture.Service.UpdateAsync(
            external.Guid,
            new UpdateEmployeeRequest(
                external.Nome, external.Email, null,
                fixture.Profession.Guid, fixture.Position.Guid, fixture.Level.Guid),
            context,
            CancellationToken.None);

        Assert.DoesNotContain(page.Items, x => x.Guid == external.Guid);
        Assert.Null(found);
        Assert.Null(updated);
    }
}

internal sealed class EmployeeFixture : IAsyncDisposable
{
    private EmployeeFixture(AppDbContext db, EmployeeService service)
    {
        Db = db;
        Service = service;
    }

    public AppDbContext Db { get; }
    public EmployeeService Service { get; }
    public UnidadeHospitalar UnitA { get; private set; } = null!;
    public UnidadeHospitalar UnitB { get; private set; } = null!;
    public UnidadeHospitalar OtherOrganizationUnit { get; private set; } = null!;
    public Setor SectorA { get; private set; } = null!;
    public Profissao Profession { get; private set; } = null!;
    public Cargo Position { get; private set; } = null!;
    public NivelProfissional Level { get; private set; } = null!;
    public Guid ActorUserGuid { get; private set; }

    public static async Task<EmployeeFixture> CreateAsync()
    {
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, clock);
        var fixture = new EmployeeFixture(db, new EmployeeService(db, clock));
        await fixture.SeedAsync(clock.GetUtcNow());
        return fixture;
    }

    public CreateEmployeeRequest CreateRequest(
        Guid hiringUnitGuid,
        IReadOnlyCollection<CreateEmployeeActingUnitRequest> actingUnits,
        IReadOnlyCollection<CreateEmployeeSectorRequest> sectors) =>
        new(
            "Maria da Silva",
            $"maria-{Guid.NewGuid():N}@hospital.test",
            "+55 11 99999-0000",
            Profession.Guid,
            Position.Guid,
            Level.Guid,
            hiringUnitGuid,
            new DateOnly(2026, 1, 2),
            actingUnits,
            sectors);

    public EmployeeOperationContext Context(Guid correlationId) =>
        new(ActorUserGuid, correlationId, "0123456789abcdef", "127.0.0.1");

    private async Task SeedAsync(DateTimeOffset now)
    {
        var organization = new Organizacao(Guid.NewGuid(), "Rede Hospitalar", now);
        var otherOrganization = new Organizacao(Guid.NewGuid(), "Outra Rede", now);
        Db.Organizacoes.AddRange(organization, otherOrganization);
        await Db.SaveChangesAsync();

        UnitA = new UnidadeHospitalar(Guid.NewGuid(), organization.Id, "Hospital A", now);
        UnitB = new UnidadeHospitalar(Guid.NewGuid(), organization.Id, "Hospital B", now);
        OtherOrganizationUnit = new UnidadeHospitalar(
            Guid.NewGuid(), otherOrganization.Id, "Hospital Externo", now);
        Db.UnidadesHospitalares.AddRange(UnitA, UnitB, OtherOrganizationUnit);
        await Db.SaveChangesAsync();

        SectorA = new Setor(Guid.NewGuid(), UnitA.Id, "Farmácia", now);
        Profession = new Profissao(Guid.NewGuid(), "Farmacêutico", null, now);
        Position = new Cargo(Guid.NewGuid(), "Farmacêutico Clínico", null, now);
        Level = new NivelProfissional(Guid.NewGuid(), "SR", "Sênior", 3, now);
        Db.AddRange(SectorA, Profession, Position, Level);
        await Db.SaveChangesAsync();

        var actorEmployee = new Funcionario(
            Guid.NewGuid(), "Gestor da Rede", "gestor@hospital.test", null,
            Profession.Id, Position.Id, Level.Id, UnitB.Id, new DateOnly(2025, 1, 1), now);
        Db.Funcionarios.Add(actorEmployee);
        await Db.SaveChangesAsync();
        var actor = new Usuario(Guid.NewGuid(), actorEmployee.Id, actorEmployee.Email, "HASH_DE_TESTE", now);
        Db.Usuarios.Add(actor);
        await Db.SaveChangesAsync();
        ActorUserGuid = actor.Guid;
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}

internal sealed class TestTimeProvider(DateTimeOffset current) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => current;
}
