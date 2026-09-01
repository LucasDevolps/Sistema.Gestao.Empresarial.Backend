using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;
using Sistema.Gestao.Empresarial.Infrastructure.Security;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Authentication;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task LoginValido_DevePersistirSessaoAuditoriaEOutbox()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();

        var response = await fixture.Service.LoginAsync(
            new LoginRequest(fixture.Email, fixture.Password),
            fixture.Context,
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Single(await fixture.Db.UsuariosSessoes.Where(x => x.Ativo && !x.Revogado).ToListAsync());
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), x => x.Acao == "LoginRealizado");
        Assert.Contains(await fixture.Db.OutboxMessages.ToListAsync(), x => x.EventType == "LoginRealizado");

        var jwt = new JsonWebToken(response.AccessToken);
        Assert.Equal(response.SessionId.ToString("D"), jwt.GetClaim("sid").Value);
        Assert.DoesNotContain(response.RefreshToken, (await fixture.Db.UsuariosSessoes.SingleAsync()).RefreshTokenHash);
    }

    [Fact]
    public async Task LoginInvalido_NaoDeveCriarSessao()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();

        var response = await fixture.Service.LoginAsync(
            new LoginRequest(fixture.Email, "senha-incorreta"),
            fixture.Context,
            CancellationToken.None);

        Assert.Null(response);
        Assert.Empty(await fixture.Db.UsuariosSessoes.ToListAsync());
        Assert.Equal(1, (await fixture.Db.Usuarios.SingleAsync()).TentativasLoginInvalidas);
    }

    [Fact]
    public async Task NovoLogin_DeveRevogarSessaoAnterior()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();
        var first = await fixture.LoginAsync();

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await fixture.LoginAsync();

        var sessions = await fixture.Db.UsuariosSessoes.OrderBy(x => x.DataCriacao).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.True(sessions[0].Revogado);
        Assert.Equal("NOVO_LOGIN", sessions[0].MotivoRevogacao);
        Assert.False(sessions[1].Revogado);
        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.False(await fixture.Service.ValidateAsync(ToClaims(first), CancellationToken.None));
        Assert.True(await fixture.Service.ValidateAsync(ToClaims(second), CancellationToken.None));
    }

    [Fact]
    public async Task LogoutRepetido_DeveSerIdempotente()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();
        var authentication = await fixture.LoginAsync();
        var claims = ToClaims(authentication);

        await fixture.Service.LogoutAsync(claims, fixture.Context, CancellationToken.None);
        await fixture.Service.LogoutAsync(claims, fixture.Context, CancellationToken.None);

        var session = await fixture.Db.UsuariosSessoes.SingleAsync();
        Assert.True(session.Revogado);
        Assert.Single(await fixture.Db.OutboxMessages.Where(x => x.EventType == "SessaoRevogada").ToListAsync());
        Assert.False(await fixture.Service.ValidateAsync(claims, CancellationToken.None));
    }

    [Fact]
    public async Task TrintaMinutosSemAtividade_DeveExpirarERevogarSessao()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();
        var authentication = await fixture.LoginAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));

        var valid = await fixture.Service.ValidateAsync(ToClaims(authentication), CancellationToken.None);

        Assert.False(valid);
        Assert.True((await fixture.Db.UsuariosSessoes.SingleAsync()).Revogado);
        Assert.Contains(await fixture.Db.OutboxMessages.ToListAsync(), x => x.EventType == "SessaoExpirada");
    }

    [Fact]
    public async Task RefreshRotacionado_DeveInvalidarReutilizacaoDoTokenAnterior()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();
        var login = await fixture.LoginAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        var refreshed = await fixture.Service.RefreshAsync(
            new RefreshTokenRequest(login.SessionId, login.RefreshToken), fixture.Context, CancellationToken.None);
        var reused = await fixture.Service.RefreshAsync(
            new RefreshTokenRequest(login.SessionId, login.RefreshToken), fixture.Context, CancellationToken.None);

        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Null(reused);
        Assert.True((await fixture.Db.UsuariosSessoes.SingleAsync()).Revogado);
    }

    [Fact]
    public async Task FallbackSql_NaoDeveRevogarSessaoAtualAoReceberJtiAntigo()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();
        var login = await fixture.LoginAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var refreshed = await fixture.Service.RefreshAsync(
            new RefreshTokenRequest(login.SessionId, login.RefreshToken), fixture.Context, CancellationToken.None);
        Assert.NotNull(refreshed);
        fixture.OperationalStore.Unavailable = true;

        var oldValid = await fixture.Service.ValidateAsync(ToClaims(login), CancellationToken.None);
        var currentValid = await fixture.Service.ValidateAsync(ToClaims(refreshed), CancellationToken.None);

        Assert.False(oldValid);
        Assert.True(currentValid);
        Assert.False((await fixture.Db.UsuariosSessoes.SingleAsync()).Revogado);
    }

    [Fact]
    public async Task LoginsConcorrentes_DevemManterSomenteUmaSessaoAtiva()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString("N");
        DbContextOptions<AppDbContext> CreateOptions() => new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .Options;
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero));
        var hasher = new CredentialHasher();
        const string email = "concorrente@hospital.test";
        const string password = "Senha-Forte-123!";
        await using (var seed = new AppDbContext(CreateOptions(), clock))
        {
            seed.Usuarios.Add(new Usuario(Guid.NewGuid(), null, email, hasher.HashPassword(password), clock.GetUtcNow()));
            await seed.SaveChangesAsync();
        }

        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "tests",
            Audience = "tests",
            SigningKey = "test-signing-key-with-more-than-thirty-two-characters",
            AccessTokenLifetimeMinutes = 10
        });
        var sessionOptions = Options.Create(new SessionOptions());
        var operationalStore = new FakeSessionOperationalStore(TimeSpan.FromMinutes(30));
        await using var metricProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var metrics = new AuthenticationMetrics(metricProvider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
        await using var firstContext = new AppDbContext(CreateOptions(), clock);
        await using var secondContext = new AppDbContext(CreateOptions(), clock);
        AuthenticationService CreateService(AppDbContext context) => new(
            context,
            hasher,
            new JwtTokenService(jwtOptions),
            operationalStore,
            sessionOptions,
            clock,
            metrics,
            NullLogger<AuthenticationService>.Instance);
        var operationContext = new AuthOperationContext(Guid.NewGuid(), "trace", "127.0.0.1", "tests");

        var results = await Task.WhenAll(
            CreateService(firstContext).LoginAsync(new LoginRequest(email, password), operationContext, CancellationToken.None),
            CreateService(secondContext).LoginAsync(new LoginRequest(email, password), operationContext, CancellationToken.None));

        await using var verification = new AppDbContext(CreateOptions(), clock);
        var sessions = await verification.UsuariosSessoes.ToListAsync();
        Assert.All(results, Assert.NotNull);
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, x => x.Ativo && !x.Revogado);
        Assert.Single(sessions, x => x.Revogado);
    }

    private static SessionTokenClaims ToClaims(AuthenticationResponse response)
    {
        var jwt = new JsonWebToken(response.AccessToken);
        return new SessionTokenClaims(
            response.UserGuid,
            response.SessionId,
            jwt.GetClaim("jti").Value,
            long.Parse(jwt.GetClaim("session_version").Value, System.Globalization.CultureInfo.InvariantCulture));
    }
}

internal sealed class AuthenticationFixture : IAsyncDisposable
{
    private AuthenticationFixture(
        AppDbContext db,
        AuthenticationService service,
        ManualTimeProvider clock,
        FakeSessionOperationalStore operationalStore,
        ServiceProvider serviceProvider,
        string email,
        string password)
    {
        Db = db;
        Service = service;
        Clock = clock;
        OperationalStore = operationalStore;
        ServiceProvider = serviceProvider;
        Email = email;
        Password = password;
    }

    public AppDbContext Db { get; }
    public AuthenticationService Service { get; }
    public ManualTimeProvider Clock { get; }
    public FakeSessionOperationalStore OperationalStore { get; }
    private ServiceProvider ServiceProvider { get; }
    public string Email { get; }
    public string Password { get; }
    public AuthOperationContext Context { get; } = new(
        Guid.NewGuid(), "0123456789abcdef0123456789abcdef", "127.0.0.1", "tests");

    public static async Task<AuthenticationFixture> CreateAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero));
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(dbOptions, clock);
        var hasher = new CredentialHasher();
        const string password = "Senha-Forte-123!";
        const string email = "usuario@hospital.test";
        db.Usuarios.Add(new Usuario(Guid.NewGuid(), null, email, hasher.HashPassword(password), clock.GetUtcNow()));
        await db.SaveChangesAsync();

        var jwt = Options.Create(new JwtOptions
        {
            Issuer = "tests",
            Audience = "tests",
            SigningKey = "test-signing-key-with-more-than-thirty-two-characters",
            AccessTokenLifetimeMinutes = 10
        });
        var session = Options.Create(new SessionOptions
        {
            InactivityTimeoutMinutes = 30,
            ActivityPersistenceIntervalSeconds = 60,
            AbsoluteLifetimeDays = 7,
            MaximumFailedLoginAttempts = 5,
            EnableSqlFallback = true
        });
        var operationalStore = new FakeSessionOperationalStore(TimeSpan.FromMinutes(30));
        var serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var metrics = new AuthenticationMetrics(serviceProvider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
        var service = new AuthenticationService(
            db,
            hasher,
            new JwtTokenService(jwt),
            operationalStore,
            session,
            clock,
            metrics,
            NullLogger<AuthenticationService>.Instance);
        return new AuthenticationFixture(db, service, clock, operationalStore, serviceProvider, email, password);
    }

    public async Task<AuthenticationResponse> LoginAsync() =>
        await Service.LoginAsync(new LoginRequest(Email, Password), Context, CancellationToken.None)
        ?? throw new InvalidOperationException("Login de teste falhou.");

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await ServiceProvider.DisposeAsync();
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
{
    private DateTimeOffset _current = current;
    public override DateTimeOffset GetUtcNow() => _current;
    public void Advance(TimeSpan duration) => _current = _current.Add(duration);
}

internal sealed class FakeSessionOperationalStore(TimeSpan timeout) : ISessionOperationalStore
{
    private readonly Dictionary<Guid, OperationalSession> _sessions = [];
    private readonly Dictionary<Guid, Guid> _activeSessions = [];
    public bool Unavailable { get; set; }

    public Task ReplaceActiveSessionAsync(
        OperationalSession session,
        IReadOnlyCollection<Guid> previousSessionIds,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        foreach (var previous in previousSessionIds)
        {
            _sessions.Remove(previous);
        }
        _sessions[session.SessionId] = session;
        _activeSessions[session.UserGuid] = session.SessionId;
        return Task.CompletedTask;
    }

    public Task<OperationalSessionValidation> ValidateAndTouchAsync(
        SessionTokenClaims claims,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (!_activeSessions.TryGetValue(claims.UserGuid, out var active) || active != claims.SessionId)
        {
            return Task.FromResult(OperationalSessionValidation.Invalid);
        }
        if (!_sessions.TryGetValue(claims.SessionId, out var session))
        {
            return Task.FromResult(OperationalSessionValidation.Missing);
        }
        if (session.Jti != claims.Jti || session.SessionVersion != claims.SessionVersion)
        {
            return Task.FromResult(OperationalSessionValidation.Invalid);
        }
        if (now - session.LastActivityAt >= timeout)
        {
            _sessions.Remove(claims.SessionId);
            _activeSessions.Remove(claims.UserGuid);
            return Task.FromResult(OperationalSessionValidation.Expired);
        }
        _sessions[claims.SessionId] = session with { LastActivityAt = now };
        return Task.FromResult(OperationalSessionValidation.Valid);
    }

    public Task UpsertAsync(OperationalSession session, CancellationToken cancellationToken) =>
        ReplaceActiveSessionAsync(session, [], cancellationToken);

    public Task RotateJtiAsync(Guid sessionId, string jti, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            _sessions[sessionId] = session with { Jti = jti, LastActivityAt = now };
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid userGuid, IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        foreach (var sessionId in sessionIds)
        {
            _sessions.Remove(sessionId);
        }
        _activeSessions.Remove(userGuid);
        return Task.CompletedTask;
    }

    private void ThrowIfUnavailable()
    {
        if (Unavailable)
        {
            throw new SessionStoreUnavailableException("Redis indisponível no teste.", new InvalidOperationException());
        }
    }
}
