using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.WorkSchedules;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.WorkSchedules;

namespace Sistema.Gestao.Empresarial.IntegrationTests.WorkSchedules;

/// <summary>
/// Casos de uso de jornadas de trabalho (issue #51) sobre EF InMemory. A
/// insensibilidade a caixa do nome depende da collation do SQL Server e é coberta em
/// <c>RealInfrastructure/WorkScheduleDatabaseTests</c>.
/// </summary>
public sealed class WorkScheduleServiceTests : IAsyncDisposable
{
    private readonly AppDbContext _db;
    private readonly WorkScheduleService _service;

    public WorkScheduleServiceTests()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
            clock);
        _service = new WorkScheduleService(_db, clock);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static WorkScheduleOperationContext Context(Guid? correlation = null) =>
        new(Guid.NewGuid(), correlation ?? Guid.NewGuid(), "trace", "127.0.0.1");

    private Task<WorkScheduleResponse> CreateAsync(
        string name, int work = 6, int rest = 1, int max = 6, string? description = null) =>
        _service.CreateAsync(
            new CreateWorkScheduleRequest(name, work, rest, max, description), Context(), CancellationToken.None);

    [Fact]
    public async Task Criar_6x1_DevePersistirEGerarAuditoriaEOutbox()
    {
        var correlation = Guid.NewGuid();

        var created = await _service.CreateAsync(
            new CreateWorkScheduleRequest("  6x1 ", 6, 1, 6, "Assistencial"),
            Context(correlation),
            CancellationToken.None);

        Assert.Equal("6x1", created.Name);
        Assert.Equal(6, created.ConsecutiveWorkDays);
        Assert.Equal(1, created.RestDays);
        Assert.Equal(6, created.MaximumConsecutiveWorkDays);
        Assert.Equal("Assistencial", created.Description);
        Assert.True(created.Active);
        var audit = await _db.AuditLogs.SingleAsync(x => x.CorrelationId == correlation);
        Assert.Equal("JornadaTrabalho", audit.Entidade);
        Assert.Equal("CRIADO", audit.Acao);
        Assert.Equal(created.Guid, audit.EntidadeGuid);
        Assert.Null(audit.ValorAnterior);
        Assert.Single(await _db.OutboxMessages
            .Where(x => x.CorrelationId == correlation && x.EventType == "JornadaTrabalhoCriada")
            .ToListAsync());
    }

    [Fact]
    public async Task Criar_5x2_DevePersistir()
    {
        var created = await CreateAsync("5x2", 5, 2, 5);

        Assert.Equal((5, 2, 5), (created.ConsecutiveWorkDays, created.RestDays, created.MaximumConsecutiveWorkDays));
    }

    [Fact]
    public async Task Criar_NomeDuplicadoComEspacos_DeveRejeitarPeloCampoNameSemRepetirValor()
    {
        await CreateAsync("Plantão 4x2", 4, 2, 4);

        var error = await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() => CreateAsync("  Plantão 4x2  ", 4, 2, 4));

        Assert.Equal("name", error.Field);
        Assert.DoesNotContain("Plantão", error.Message);
        Assert.Equal(1, await _db.JornadasTrabalho.CountAsync());
    }

    [Theory]
    [InlineData(0, 1, 6)]
    [InlineData(6, 0, 6)]
    [InlineData(6, 1, 7)]
    [InlineData(6, 1, 0)]
    public async Task Criar_ParametrosInvalidos_DevemSerRejeitadosPeloDominioSemEfeitos(int work, int rest, int max)
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateAsync("X", work, rest, max));

        Assert.Empty(await _db.JornadasTrabalho.ToListAsync());
        Assert.Empty(await _db.AuditLogs.ToListAsync());
        Assert.Empty(await _db.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Atualizar_DevePreservarGuidAuditarComSnapshotEEmitirEvento()
    {
        var created = await CreateAsync("6x1");
        var correlation = Guid.NewGuid();

        var updated = await _service.UpdateAsync(
            created.Guid,
            new UpdateWorkScheduleRequest("Administrativo 5x2", 5, 2, 5, "Adm"),
            Context(correlation),
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(created.Guid, updated.Guid);
        Assert.Equal("Administrativo 5x2", updated.Name);
        Assert.Equal(5, updated.ConsecutiveWorkDays);
        var audit = await _db.AuditLogs.SingleAsync(x => x.CorrelationId == correlation);
        Assert.Equal("ATUALIZADO", audit.Acao);
        Assert.Contains("6x1", audit.ValorAnterior);
        Assert.Contains("Administrativo 5x2", audit.ValorNovo);
        Assert.Single(await _db.OutboxMessages
            .Where(x => x.CorrelationId == correlation && x.EventType == "JornadaTrabalhoAtualizada")
            .ToListAsync());
    }

    [Fact]
    public async Task Atualizar_SemMudancas_NaoGeraAuditoria()
    {
        var created = await CreateAsync("6x1");
        var correlation = Guid.NewGuid();

        await _service.UpdateAsync(
            created.Guid, new UpdateWorkScheduleRequest("6x1", 6, 1, 6, null), Context(correlation), CancellationToken.None);

        Assert.Empty(await _db.AuditLogs.Where(x => x.CorrelationId == correlation).ToListAsync());
    }

    [Fact]
    public async Task Atualizar_ComNomeDeOutraJornada_DeveRejeitar_EMesmoNomeDaPropriaJornadaNao()
    {
        var first = await CreateAsync("6x1");
        await CreateAsync("5x2", 5, 2, 5);

        await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() => _service.UpdateAsync(
            first.Guid, new UpdateWorkScheduleRequest(" 5x2 ", 6, 1, 6, null), Context(), CancellationToken.None));
        var same = await _service.UpdateAsync(
            first.Guid, new UpdateWorkScheduleRequest("6x1", 6, 1, 6, "nova descrição"), Context(), CancellationToken.None);
        Assert.Equal("nova descrição", same!.Description);
    }

    [Fact]
    public async Task Atualizar_ParametrosInvalidos_DevemSerRejeitados()
    {
        var created = await CreateAsync("6x1");

        await Assert.ThrowsAsync<DomainException>(() => _service.UpdateAsync(
            created.Guid, new UpdateWorkScheduleRequest("6x1", 6, 1, 7, null), Context(), CancellationToken.None));
        Assert.Equal(6, (await _service.GetAsync(created.Guid, CancellationToken.None))!.MaximumConsecutiveWorkDays);
    }

    [Fact]
    public async Task InativarEReativar_DevemManterRegistroAuditarEPermitirConsulta()
    {
        var created = await CreateAsync("6x1");
        var inactivateCorrelation = Guid.NewGuid();
        var reactivateCorrelation = Guid.NewGuid();

        var inactive = await _service.ChangeStatusAsync(created.Guid, false, Context(inactivateCorrelation), CancellationToken.None);
        var stillThere = await _service.GetAsync(created.Guid, CancellationToken.None);
        var reactivated = await _service.ChangeStatusAsync(created.Guid, true, Context(reactivateCorrelation), CancellationToken.None);

        Assert.False(inactive!.Active);
        Assert.False(stillThere!.Active);
        Assert.True(reactivated!.Active);
        Assert.Equal("INATIVADO", (await _db.AuditLogs.SingleAsync(x => x.CorrelationId == inactivateCorrelation)).Acao);
        Assert.Equal("REATIVADO", (await _db.AuditLogs.SingleAsync(x => x.CorrelationId == reactivateCorrelation)).Acao);
        Assert.Single(await _db.OutboxMessages.Where(x => x.EventType == "JornadaTrabalhoInativada").ToListAsync());
        Assert.Single(await _db.OutboxMessages.Where(x => x.EventType == "JornadaTrabalhoReativada").ToListAsync());
        Assert.Equal(1, await _db.JornadasTrabalho.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task AlterarStatus_MesmoStatus_EIdempotenteSemAuditoria()
    {
        var created = await CreateAsync("6x1");
        var correlation = Guid.NewGuid();

        var result = await _service.ChangeStatusAsync(created.Guid, true, Context(correlation), CancellationToken.None);

        Assert.True(result!.Active);
        Assert.Empty(await _db.AuditLogs.Where(x => x.CorrelationId == correlation).ToListAsync());
    }

    [Fact]
    public async Task OperacoesEmJornadaInexistente_RetornamNulo()
    {
        var guid = Guid.NewGuid();

        Assert.Null(await _service.GetAsync(guid, CancellationToken.None));
        Assert.Null(await _service.UpdateAsync(guid, new UpdateWorkScheduleRequest("X", 6, 1, 6, null), Context(), CancellationToken.None));
        Assert.Null(await _service.ChangeStatusAsync(guid, false, Context(), CancellationToken.None));
    }

    [Fact]
    public async Task Listar_DeveFiltrarPorNomeESituacaoEPaginarNoBanco()
    {
        await CreateAsync("6x1");
        await CreateAsync("5x2", 5, 2, 5);
        var inactive = await CreateAsync("Administrativo 5x2", 5, 2, 5);
        await CreateAsync("12x36", 1, 1, 1);
        await _service.ChangeStatusAsync(inactive.Guid, false, Context(), CancellationToken.None);

        var all = await _service.ListAsync(new WorkScheduleListQuery(null, null), CancellationToken.None);
        var actives = await _service.ListAsync(new WorkScheduleListQuery(null, true), CancellationToken.None);
        var inactives = await _service.ListAsync(new WorkScheduleListQuery(null, false), CancellationToken.None);
        var byName = await _service.ListAsync(new WorkScheduleListQuery(" 5x2 ", null), CancellationToken.None);
        var page = await _service.ListAsync(new WorkScheduleListQuery(null, null, 2, 3), CancellationToken.None);

        Assert.Equal(4, all.Total);
        Assert.Equal(["12x36", "5x2", "6x1", "Administrativo 5x2"], all.Items.Select(x => x.Name).ToArray());
        Assert.Equal(3, actives.Total);
        Assert.Equal(["Administrativo 5x2"], inactives.Items.Select(x => x.Name).ToArray());
        Assert.Equal(2, byName.Total);
        Assert.Equal(4, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(2, page.Page);
    }

    [Fact]
    public async Task Listar_NaoDeveIncluirJornadaExcluidaLogicamente()
    {
        var created = await CreateAsync("6x1");
        var entity = await _db.JornadasTrabalho.SingleAsync(x => x.Guid == created.Guid);
        entity.ExcluirLogicamente(Guid.NewGuid(), DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync();

        Assert.Empty((await _service.ListAsync(new WorkScheduleListQuery(null, null), CancellationToken.None)).Items);
        Assert.Null(await _service.GetAsync(created.Guid, CancellationToken.None));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
