using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Caching;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Security;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed class AuthenticationConcurrencyTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task LoginsConcorrentes_DevemManterSomenteASessaoMaisRecente()
    {
        var email = $"login-{fixture.IsolationKey}@hospital.test";
        const string password = "Senha forte de integração 2026!";
        var hasher = new CredentialHasher();
        Guid userGuid;
        await using (var setup = fixture.CreateDbContext())
        {
            var user = new Usuario(
                Guid.NewGuid(), null, email, hasher.HashPassword(password), TimeProvider.System.GetUtcNow());
            setup.Usuarios.Add(user);
            await setup.SaveChangesAsync();
            userGuid = user.Guid;
        }

        var sessionOptions = Options.Create(new SessionOptions
        {
            InactivityTimeoutMinutes = 30,
            ActivityPersistenceIntervalSeconds = 60,
            AbsoluteLifetimeDays = 7,
            MaximumFailedLoginAttempts = 5,
            EnableSqlFallback = true
        });
        var tokenService = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "sge-real-tests",
            Audience = "sge-real-tests",
            SigningKey = "real-integration-tests-signing-key-with-more-than-32-characters",
            AccessTokenLifetimeMinutes = 10
        }));
        var operationalStore = new RedisSessionOperationalStore(
            fixture.Redis,
            new IsolatedRedisKeys(fixture.IsolationKey),
            sessionOptions);
        var metrics = new AuthenticationMetrics(
            fixture.Metrics.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        await using var firstDb = fixture.CreateDbContext();
        await using var secondDb = fixture.CreateDbContext();
        var firstService = CreateService(firstDb);
        var secondService = CreateService(secondDb);
        var login = new LoginRequest(email, password);

        var responses = await Task.WhenAll(
            firstService.LoginAsync(login, Context(), CancellationToken.None),
            secondService.LoginAsync(login, Context(), CancellationToken.None));

        Assert.All(responses, Assert.NotNull);
        await using var verification = fixture.CreateDbContext();
        var sessions = await verification.UsuariosSessoes.AsNoTracking()
            .Include(x => x.Usuario)
            .Where(x => x.Usuario.Guid == userGuid)
            .OrderBy(x => x.DataCriacao)
            .ToListAsync();
        Assert.Equal(2, sessions.Count);
        var activeSession = Assert.Single(sessions, x => x.Ativo && !x.Revogado);
        var revokedSession = Assert.Single(sessions, x => x.Revogado);

        var activeResult = await operationalStore.ValidateAndTouchAsync(
            new SessionTokenClaims(userGuid, activeSession.SessionId, activeSession.Jti, activeSession.VersaoSessao),
            TimeProvider.System.GetUtcNow(),
            CancellationToken.None);
        var revokedResult = await operationalStore.ValidateAndTouchAsync(
            new SessionTokenClaims(userGuid, revokedSession.SessionId, revokedSession.Jti, revokedSession.VersaoSessao),
            TimeProvider.System.GetUtcNow(),
            CancellationToken.None);
        Assert.Contains(activeResult, new[]
        {
            OperationalSessionValidation.Valid,
            OperationalSessionValidation.CheckpointRequired
        });
        Assert.NotEqual(OperationalSessionValidation.Valid, revokedResult);

        var activeResponse = Assert.Single(responses, x => x!.SessionId == activeSession.SessionId)!;
        await using var refreshDb = fixture.CreateDbContext();
        var refreshService = CreateService(refreshDb);
        var refreshed = await refreshService.RefreshAsync(
            new RefreshTokenRequest(activeResponse.SessionId, activeResponse.RefreshToken),
            Context(),
            CancellationToken.None);
        Assert.NotNull(refreshed);

        await using var refreshedSessionDb = fixture.CreateDbContext();
        var refreshedSession = await refreshedSessionDb.UsuariosSessoes.AsNoTracking()
            .Include(x => x.Usuario)
            .SingleAsync(x => x.SessionId == activeSession.SessionId);

        await using var logoutDb = fixture.CreateDbContext();
        var logoutService = CreateService(logoutDb);
        await logoutService.LogoutAsync(
            new SessionTokenClaims(
                userGuid,
                refreshedSession.SessionId,
                refreshedSession.Jti,
                refreshedSession.VersaoSessao),
            Context(),
            CancellationToken.None);

        await using var logoutVerification = fixture.CreateDbContext();
        Assert.Empty(await logoutVerification.UsuariosSessoes.AsNoTracking()
            .Where(x => x.UsuarioId == refreshedSession.UsuarioId && x.Ativo && !x.Revogado)
            .ToListAsync());

        AuthenticationService CreateService(AppDbContext db) =>
            new(
                db,
                hasher,
                tokenService,
                operationalStore,
                sessionOptions,
                TimeProvider.System,
                metrics,
                NullLogger<AuthenticationService>.Instance);
    }

    private static AuthOperationContext Context() =>
        new(Guid.NewGuid(), Guid.NewGuid().ToString("N"), "127.0.0.1", "real-infrastructure-test");

    private sealed class IsolatedRedisKeys(string isolationKey) : IRedisKeyFactory
    {
        private readonly string _prefix = $"sge:integration:{isolationKey}";

        public string Session(Guid sessionId) => $"{_prefix}:session:{sessionId:N}";
        public string ActiveSession(Guid userGuid) => $"{_prefix}:user:{userGuid:N}:active-session";
        public string Permissions(Guid userGuid) => $"{_prefix}:permissions:{userGuid:N}";
        public string DirtySessionActivity() => $"{_prefix}:session-activity:dirty";
    }
}
