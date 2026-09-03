using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Bootstrap;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Infrastructure.Bootstrap;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Security;
using Sistema.Gestao.Empresarial.IntegrationTests.Authentication;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Bootstrap;

public sealed class InitialAdminBootstrapTests
{
    private const string StrongPassword = "Uma-Senha-Forte-2026!";

    [Fact]
    public async Task BootstrapValido_DeveCriarFundacaoCompletaAuditadaSemExporSenha()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();

        var result = await fixture.Service.ExecuteAsync(ValidRequest(), CancellationToken.None);

        var user = await fixture.Db.Usuarios.SingleAsync();
        var employee = await fixture.Db.Funcionarios.SingleAsync();
        var profile = await fixture.Db.Perfis.SingleAsync();
        Assert.Equal(result.UserGuid, user.Guid);
        Assert.Equal(employee.Id, user.FuncionarioId);
        Assert.True(fixture.Hasher.VerifyHashedPassword(user.SenhaHash, StrongPassword));
        Assert.NotEqual(StrongPassword, user.SenhaHash);
        Assert.Equal("ADMINISTRADOR_INICIAL", profile.Nome);
        Assert.Equal(
            await fixture.Db.Permissoes.CountAsync(permission => permission.Ativo),
            result.GrantedPermissionCount);
        Assert.Equal(result.GrantedPermissionCount, await fixture.Db.PerfisPermissoes.CountAsync());
        Assert.Single(await fixture.Db.UsuariosPerfis.ToListAsync());

        var audit = await fixture.Db.AuditLogs.SingleAsync();
        var outbox = await fixture.Db.OutboxMessages.SingleAsync();
        Assert.Equal("ADMINISTRADOR_INICIAL_CRIADO", audit.Acao);
        Assert.Equal("AdministradorInicialCriado", outbox.EventType);
        Assert.DoesNotContain(StrongPassword, audit.ValorNovo, StringComparison.Ordinal);
        Assert.DoesNotContain(StrongPassword, outbox.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SegundaExecucao_DeveFalharFechadaSemAlterarDados()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();
        await fixture.Service.ExecuteAsync(ValidRequest(), CancellationToken.None);

        await Assert.ThrowsAsync<InitialAdminAlreadyProvisionedException>(() =>
            fixture.Service.ExecuteAsync(ValidRequest() with
            {
                AdministratorEmail = "outro@hospital.test"
            }, CancellationToken.None));

        Assert.Single(await fixture.Db.Usuarios.ToListAsync());
        Assert.Single(await fixture.Db.Organizacoes.ToListAsync());
        Assert.Single(await fixture.Db.AuditLogs.ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task SenhaFraca_DeveSerRejeitadaAntesDeQualquerPersistencia()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.ExecuteAsync(
            ValidRequest() with { Password = "senha-fraca" },
            CancellationToken.None));

        Assert.Empty(await fixture.Db.Usuarios.ToListAsync());
        Assert.Empty(await fixture.Db.Organizacoes.ToListAsync());
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task CatalogoDePermissoesIncompleto_DeveFalharAntesDeCriarOrganizacao()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();
        var requiredPermission = await fixture.Db.Permissoes.SingleAsync(permission =>
            permission.Codigo == PermissionCodes.ManageUserPermissions);
        requiredPermission.Inativar(fixture.Clock.GetUtcNow());
        await fixture.Db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ExecuteAsync(ValidRequest(), CancellationToken.None));

        Assert.Contains(PermissionCodes.ManageUserPermissions, exception.Message, StringComparison.Ordinal);
        Assert.Empty(await fixture.Db.Organizacoes.ToListAsync());
        Assert.Empty(await fixture.Db.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task UsuarioExcluidoLogicamente_DeveImpedirNovoBootstrap()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();
        var existing = new Usuario(
            Guid.NewGuid(), null, "antigo@hospital.test", "HASH_DE_TESTE", fixture.Clock.GetUtcNow());
        existing.ExcluirLogicamente(Guid.NewGuid(), fixture.Clock.GetUtcNow());
        fixture.Db.Usuarios.Add(existing);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InitialAdminAlreadyProvisionedException>(() =>
            fixture.Service.ExecuteAsync(ValidRequest(), CancellationToken.None));

        Assert.Empty(await fixture.Db.Usuarios.ToListAsync());
        Assert.Single(await fixture.Db.Usuarios.IgnoreQueryFilters().ToListAsync());
    }

    private static InitialAdminBootstrapRequest ValidRequest() => new(
        "Organização Hospitalar Teste",
        "Hospital Central",
        "Administração Hospitalar",
        "Administrador do Sistema",
        "SR",
        "Administrador Inicial",
        "admin@hospital.test",
        "+55 11 99999-0000",
        new DateOnly(2026, 1, 1),
        StrongPassword);
}

internal sealed class BootstrapFixture : IAsyncDisposable
{
    private BootstrapFixture(
        AppDbContext db,
        ManualTimeProvider clock,
        CredentialHasher hasher,
        InitialAdminBootstrapService service)
    {
        Db = db;
        Clock = clock;
        Hasher = hasher;
        Service = service;
    }

    public AppDbContext Db { get; }
    public ManualTimeProvider Clock { get; }
    public CredentialHasher Hasher { get; }
    public InitialAdminBootstrapService Service { get; }

    public static async Task<BootstrapFixture> CreateAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, clock);
        db.NiveisProfissionais.Add(new NivelProfissional(
            Guid.NewGuid(), "SR", "Sênior", 3, clock.GetUtcNow()));
        db.Permissoes.AddRange(PermissionCodes.All.Select(code =>
            new Permissao(Guid.NewGuid(), code, $"Permissão {code}", clock.GetUtcNow())));
        await db.SaveChangesAsync();
        var hasher = new CredentialHasher();
        var service = new InitialAdminBootstrapService(
            db,
            hasher,
            new InitialAdminBootstrapRequestValidator(),
            clock);
        return new BootstrapFixture(db, clock, hasher, service);
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
