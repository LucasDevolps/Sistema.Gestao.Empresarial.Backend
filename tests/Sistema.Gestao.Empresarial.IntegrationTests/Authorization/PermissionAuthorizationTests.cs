using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Infrastructure.Authorization;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.IntegrationTests.Authentication;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Authorization;

public sealed class PermissionAuthorizationTests
{
    [Fact]
    public async Task NegacaoDireta_DevePrevalecerSobrePermissaoDoPerfil()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var user = await fixture.AddUserAsync("usuario@hospital.test");
        var permission = await fixture.AddPermissionAsync("FUNCIONARIO_EDITAR");
        var profile = new Perfil(Guid.NewGuid(), "Gestor", null, fixture.Clock.GetUtcNow());
        fixture.Db.Perfis.Add(profile);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.UsuariosPerfis.Add(new UsuarioPerfil(Guid.NewGuid(), user.Id, profile.Id, fixture.Clock.GetUtcNow()));
        fixture.Db.PerfisPermissoes.Add(new PerfilPermissao(Guid.NewGuid(), profile.Id, permission.Id, fixture.Clock.GetUtcNow()));
        fixture.Db.UsuariosPermissoes.Add(new UsuarioPermissao(
            Guid.NewGuid(), user.Id, permission.Id, false, fixture.Clock.GetUtcNow()));
        await fixture.Db.SaveChangesAsync();

        var granted = await fixture.Checker.HasPermissionAsync(user.Guid, permission.Codigo, CancellationToken.None);

        Assert.False(granted);
    }

    [Fact]
    public async Task CacheIndisponivel_DeveConsultarSql()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var user = await fixture.AddUserAsync("fallback@hospital.test");
        var permission = await fixture.AddPermissionAsync("SETOR_VISUALIZAR");
        fixture.Db.UsuariosPermissoes.Add(new UsuarioPermissao(
            Guid.NewGuid(), user.Id, permission.Id, true, fixture.Clock.GetUtcNow()));
        await fixture.Db.SaveChangesAsync();
        fixture.Cache.Unavailable = true;

        var granted = await fixture.Checker.HasPermissionAsync(user.Guid, permission.Codigo, CancellationToken.None);

        Assert.True(granted);
    }

    [Fact]
    public async Task BarreiraMaisNovaQueSql_DeveNegarPorPadrao()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var user = await fixture.AddUserAsync("barreira@hospital.test");
        var permission = await fixture.AddPermissionAsync("SETOR_EDITAR");
        fixture.Db.UsuariosPermissoes.Add(new UsuarioPermissao(
            Guid.NewGuid(), user.Id, permission.Id, true, fixture.Clock.GetUtcNow()));
        await fixture.Db.SaveChangesAsync();
        await fixture.Cache.AdvanceVersionAsync(user.Guid, 1, CancellationToken.None);

        var granted = await fixture.Checker.HasPermissionAsync(user.Guid, permission.Codigo, CancellationToken.None);

        Assert.False(granted);
    }

    [Fact]
    public async Task Administrador_NaoPodeAlterarAsPropriasPermissoes()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var actor = await fixture.AddUserAsync("admin@hospital.test");
        var manage = await fixture.AddPermissionAsync(PermissionCodes.ManageUserPermissions);
        fixture.Db.UsuariosPermissoes.Add(new UsuarioPermissao(
            Guid.NewGuid(), actor.Id, manage.Id, true, fixture.Clock.GetUtcNow()));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Administration.SetDirectPermissionAsync(
            actor.Guid, actor.Guid, manage.Codigo, true,
            Guid.NewGuid(), "trace", "127.0.0.1", CancellationToken.None);

        Assert.Equal(PermissionChangeResult.Forbidden, result);
        Assert.Empty(await fixture.Db.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Administrador_NaoPodeConcederPermissaoQueNaoPossui()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var actor = await fixture.AddUserAsync("admin@hospital.test");
        var target = await fixture.AddUserAsync("target@hospital.test");
        var manage = await fixture.AddPermissionAsync(PermissionCodes.ManageUserPermissions);
        var restricted = await fixture.AddPermissionAsync("FUNCIONARIO_EDITAR");
        fixture.Db.UsuariosPermissoes.Add(new UsuarioPermissao(
            Guid.NewGuid(), actor.Id, manage.Id, true, fixture.Clock.GetUtcNow()));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Administration.SetDirectPermissionAsync(
            actor.Guid, target.Guid, restricted.Codigo, true,
            Guid.NewGuid(), "trace", "127.0.0.1", CancellationToken.None);

        Assert.Equal(PermissionChangeResult.Forbidden, result);
        Assert.Empty(await fixture.Db.UsuariosPermissoes.Where(x => x.UsuarioId == target.Id).ToListAsync());
    }

    [Fact]
    public async Task AlteracaoValida_DeveVersionarAuditarInvalidarESerIdempotente()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var actor = await fixture.AddUserAsync("admin@hospital.test");
        var target = await fixture.AddUserAsync("target@hospital.test");
        var manage = await fixture.AddPermissionAsync(PermissionCodes.ManageUserPermissions);
        var delegated = await fixture.AddPermissionAsync("FUNCIONARIO_VISUALIZAR");
        fixture.Db.UsuariosPermissoes.AddRange(
            new UsuarioPermissao(Guid.NewGuid(), actor.Id, manage.Id, true, fixture.Clock.GetUtcNow()),
            new UsuarioPermissao(Guid.NewGuid(), actor.Id, delegated.Id, true, fixture.Clock.GetUtcNow()));
        await fixture.Db.SaveChangesAsync();

        var first = await fixture.Administration.SetDirectPermissionAsync(
            actor.Guid, target.Guid, delegated.Codigo, true,
            Guid.NewGuid(), "trace", "127.0.0.1", CancellationToken.None);
        var second = await fixture.Administration.SetDirectPermissionAsync(
            actor.Guid, target.Guid, delegated.Codigo, true,
            Guid.NewGuid(), "trace", "127.0.0.1", CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var updatedTarget = await fixture.Db.Usuarios.SingleAsync(x => x.Id == target.Id);
        Assert.Equal(PermissionChangeResult.Changed, first);
        Assert.Equal(PermissionChangeResult.Unchanged, second);
        Assert.Equal(1, updatedTarget.VersaoPermissoes);
        Assert.Single(await fixture.Db.UsuariosPermissoes.Where(x => x.UsuarioId == target.Id).ToListAsync());
        Assert.Single(await fixture.Db.AuditLogs.Where(x => x.Acao == "PERMISSAO_CONCEDIDA").ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages.Where(x => x.EventType == "UsuarioPermissaoAlterada").ToListAsync());
        var barrier = await fixture.Cache.GetAsync(target.Guid, CancellationToken.None);
        Assert.NotNull(barrier);
        Assert.Equal(1, barrier.Version);
        Assert.False(barrier.Ready);
    }

    [Fact]
    public async Task Administrador_NaoPodeGerenciarUsuarioDeOutraOrganizacao()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var actor = await fixture.AddUserAsync("admin@hospital.test");
        var target = await fixture.AddUserAsync("externo@hospital.test", otherOrganization: true);
        var manage = await fixture.AddPermissionAsync(PermissionCodes.ManageUserPermissions);
        fixture.Db.UsuariosPermissoes.Add(new UsuarioPermissao(
            Guid.NewGuid(), actor.Id, manage.Id, true, fixture.Clock.GetUtcNow()));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Administration.SetDirectPermissionAsync(
            actor.Guid, target.Guid, manage.Codigo, false,
            Guid.NewGuid(), "trace", "127.0.0.1", CancellationToken.None);

        Assert.Equal(PermissionChangeResult.Forbidden, result);
    }
}

internal sealed class PermissionFixture : IAsyncDisposable
{
    private PermissionFixture(
        AppDbContext db,
        ManualTimeProvider clock,
        FakePermissionCache cache,
        PermissionChecker checker,
        PermissionAdministrationService administration,
        ServiceProvider metricsProvider)
    {
        Db = db;
        Clock = clock;
        Cache = cache;
        Checker = checker;
        Administration = administration;
        MetricsProvider = metricsProvider;
    }

    public AppDbContext Db { get; }
    public ManualTimeProvider Clock { get; }
    public FakePermissionCache Cache { get; }
    public PermissionChecker Checker { get; }
    public PermissionAdministrationService Administration { get; }
    private ServiceProvider MetricsProvider { get; }
    private UnidadeHospitalar PrimaryUnit { get; set; } = null!;
    private UnidadeHospitalar OtherUnit { get; set; } = null!;
    private Profissao Profession { get; set; } = null!;
    private Cargo Position { get; set; } = null!;
    private NivelProfissional Level { get; set; } = null!;

    public static async Task<PermissionFixture> CreateAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero));
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, clock);
        var cache = new FakePermissionCache();
        var metricsProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var metrics = new PermissionMetrics(metricsProvider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
        var checker = new PermissionChecker(db, cache, metrics, NullLogger<PermissionChecker>.Instance);
        var administration = new PermissionAdministrationService(db, checker, cache, metrics, clock);
        var fixture = new PermissionFixture(db, clock, cache, checker, administration, metricsProvider);
        await fixture.SeedOrganizationAsync();
        return fixture;
    }

    public async Task<Usuario> AddUserAsync(string email, bool otherOrganization = false)
    {
        var employee = new Funcionario(
            Guid.NewGuid(), $"Usuário {Guid.NewGuid():N}", email, null,
            Profession.Id, Position.Id, Level.Id,
            otherOrganization ? OtherUnit.Id : PrimaryUnit.Id,
            new DateOnly(2025, 1, 1), Clock.GetUtcNow());
        Db.Funcionarios.Add(employee);
        await Db.SaveChangesAsync();
        var user = new Usuario(Guid.NewGuid(), employee.Id, email, "HASH_DE_TESTE", Clock.GetUtcNow());
        Db.Usuarios.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    private async Task SeedOrganizationAsync()
    {
        var now = Clock.GetUtcNow();
        var primary = new Organizacao(Guid.NewGuid(), "Rede principal", now);
        var other = new Organizacao(Guid.NewGuid(), "Outra rede", now);
        Db.Organizacoes.AddRange(primary, other);
        await Db.SaveChangesAsync();
        PrimaryUnit = new UnidadeHospitalar(Guid.NewGuid(), primary.Id, "Hospital principal", now);
        OtherUnit = new UnidadeHospitalar(Guid.NewGuid(), other.Id, "Hospital externo", now);
        Profession = new Profissao(Guid.NewGuid(), "Profissão de teste", null, now);
        Position = new Cargo(Guid.NewGuid(), "Cargo de teste", null, now);
        Level = new NivelProfissional(Guid.NewGuid(), "TS", "Teste", 1, now);
        Db.AddRange(PrimaryUnit, OtherUnit, Profession, Position, Level);
        await Db.SaveChangesAsync();
    }

    public async Task<Permissao> AddPermissionAsync(string code)
    {
        var permission = new Permissao(Guid.NewGuid(), code, $"Permissão {code}", Clock.GetUtcNow());
        Db.Permissoes.Add(permission);
        await Db.SaveChangesAsync();
        return permission;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await MetricsProvider.DisposeAsync();
    }
}

internal sealed class FakePermissionCache : IPermissionCache
{
    private readonly Dictionary<Guid, PermissionCacheEntry> _entries = [];
    public bool Unavailable { get; set; }

    public Task<PermissionCacheEntry?> GetAsync(Guid userGuid, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        return Task.FromResult(_entries.GetValueOrDefault(userGuid));
    }

    public Task<bool> PublishAsync(Guid userGuid, PermissionCacheEntry entry, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (_entries.TryGetValue(userGuid, out var current) && current.Version > entry.Version)
        {
            return Task.FromResult(false);
        }
        _entries[userGuid] = entry;
        return Task.FromResult(true);
    }

    public Task AdvanceVersionAsync(Guid userGuid, long version, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (_entries.TryGetValue(userGuid, out var current) && current.Version > version)
        {
            throw new InvalidOperationException("Versão mais recente já existe.");
        }
        _entries[userGuid] = new PermissionCacheEntry(version, false, []);
        return Task.CompletedTask;
    }

    private void ThrowIfUnavailable()
    {
        if (Unavailable)
        {
            throw new PermissionCacheUnavailableException("Cache indisponível no teste.", new InvalidOperationException());
        }
    }
}
