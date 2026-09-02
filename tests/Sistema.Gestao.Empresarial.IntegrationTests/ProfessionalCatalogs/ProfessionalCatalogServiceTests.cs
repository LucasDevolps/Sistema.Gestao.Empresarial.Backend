using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.ProfessionalCatalogs;
using EmployeeFixture = Sistema.Gestao.Empresarial.IntegrationTests.Employees.EmployeeFixture;

namespace Sistema.Gestao.Empresarial.IntegrationTests.ProfessionalCatalogs;

public sealed class ProfessionalCatalogServiceTests
{
    [Fact]
    public async Task CriarProfissaoECargo_DevePersistirAuditoriaEOutbox()
    {
        await using var fixture = CatalogFixture.Create();
        var professionCorrelation = Guid.NewGuid();
        var positionCorrelation = Guid.NewGuid();

        var profession = await fixture.Service.CreateProfessionAsync(
            new CreateProfessionalCatalogRequest("Farmacêutico", "Assistência farmacêutica"),
            fixture.Context(professionCorrelation),
            CancellationToken.None);
        var position = await fixture.Service.CreatePositionAsync(
            new CreateProfessionalCatalogRequest("Farmacêutico Clínico", null),
            fixture.Context(positionCorrelation),
            CancellationToken.None);

        Assert.True(profession.Active);
        Assert.True(position.Active);
        Assert.Single(await fixture.Db.AuditLogs
            .Where(x => x.CorrelationId == professionCorrelation && x.Entidade == "Profissao")
            .ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages
            .Where(x => x.CorrelationId == professionCorrelation && x.EventType == "ProfissaoCriada")
            .ToListAsync());
        Assert.Single(await fixture.Db.AuditLogs
            .Where(x => x.CorrelationId == positionCorrelation && x.Entidade == "Cargo")
            .ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages
            .Where(x => x.CorrelationId == positionCorrelation && x.EventType == "CargoCriado")
            .ToListAsync());
    }

    [Fact]
    public async Task AtualizarEInativarRepetidamente_DeveSerIdempotente()
    {
        await using var fixture = CatalogFixture.Create();
        var profession = await fixture.Service.CreateProfessionAsync(
            new CreateProfessionalCatalogRequest("Enfermeiro", null),
            fixture.Context(Guid.NewGuid()),
            CancellationToken.None);

        await fixture.Service.UpdateProfessionAsync(
            profession.Guid,
            new UpdateProfessionalCatalogRequest("Enfermeiro Assistencial", "Unidade de internação"),
            fixture.Context(Guid.NewGuid()),
            CancellationToken.None);
        await fixture.Service.UpdateProfessionAsync(
            profession.Guid,
            new UpdateProfessionalCatalogRequest("Enfermeiro Assistencial", "Unidade de internação"),
            fixture.Context(Guid.NewGuid()),
            CancellationToken.None);
        await fixture.Service.ChangeProfessionStatusAsync(
            profession.Guid, false, fixture.Context(Guid.NewGuid()), CancellationToken.None);
        var unchanged = await fixture.Service.ChangeProfessionStatusAsync(
            profession.Guid, false, fixture.Context(Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(unchanged);
        Assert.False(unchanged.Active);
        Assert.Single(await fixture.Db.OutboxMessages
            .Where(x => x.EventType == "ProfissaoAtualizada")
            .ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages
            .Where(x => x.EventType == "ProfissaoInativada")
            .ToListAsync());
    }

    [Fact]
    public async Task InativarCatalogoEmUsoPorFuncionarioAtivo_DeveSerRejeitadoSemEfeitos()
    {
        await using var employeeFixture = await EmployeeFixture.CreateAsync();
        await employeeFixture.Service.CreateAsync(
            employeeFixture.CreateRequest(employeeFixture.UnitB.Guid, [], []),
            employeeFixture.Context(Guid.NewGuid()),
            CancellationToken.None);
        var service = new ProfessionalCatalogService(employeeFixture.Db, TimeProvider.System);
        var initialAuditCount = await employeeFixture.Db.AuditLogs.CountAsync();
        var initialOutboxCount = await employeeFixture.Db.OutboxMessages.CountAsync();
        var context = new ProfessionalCatalogOperationContext(
            Guid.NewGuid(), Guid.NewGuid(), "catalog-test", "127.0.0.1");

        await Assert.ThrowsAsync<DomainException>(() => service.ChangeProfessionStatusAsync(
            employeeFixture.Profession.Guid, false, context, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => service.ChangePositionStatusAsync(
            employeeFixture.Position.Guid, false, context, CancellationToken.None));

        Assert.Equal(initialAuditCount, await employeeFixture.Db.AuditLogs.CountAsync());
        Assert.Equal(initialOutboxCount, await employeeFixture.Db.OutboxMessages.CountAsync());
        Assert.True(employeeFixture.Profession.Ativo);
        Assert.True(employeeFixture.Position.Ativo);
    }

    [Fact]
    public async Task ListarNiveis_DeveRetornarOrdemEstruturadaEFiltrarStatus()
    {
        await using var fixture = CatalogFixture.Create();
        var levels = await fixture.Service.ListLevelsAsync(true, CancellationToken.None);

        Assert.Equal(["JR", "PL", "SR"], levels.Select(x => x.Code));
        Assert.Equal([1, 2, 3], levels.Select(x => x.Order));
        Assert.All(levels, x => Assert.True(x.Active));
    }
}

internal sealed class CatalogFixture : IAsyncDisposable
{
    private CatalogFixture(AppDbContext db, ProfessionalCatalogService service)
    {
        Db = db;
        Service = service;
    }

    public AppDbContext Db { get; }
    public ProfessionalCatalogService Service { get; }

    public static CatalogFixture Create()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new CatalogTimeProvider(now);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, clock);
        db.NiveisProfissionais.AddRange(
            new(Guid.NewGuid(), "SR", "Sênior", 3, now),
            new(Guid.NewGuid(), "JR", "Júnior", 1, now),
            new(Guid.NewGuid(), "PL", "Pleno", 2, now));
        db.SaveChanges();
        return new CatalogFixture(db, new ProfessionalCatalogService(db, clock));
    }

    public ProfessionalCatalogOperationContext Context(Guid correlationId) =>
        new(Guid.NewGuid(), correlationId, "catalog-test", "127.0.0.1");

    public ValueTask DisposeAsync() => Db.DisposeAsync();

    private sealed class CatalogTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
    }
}
