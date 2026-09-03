using System.Data;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;

namespace Sistema.Gestao.Empresarial.Infrastructure.Security;

public sealed class AuthenticationService(
    AppDbContext dbContext,
    ICredentialHasher credentialHasher,
    ITokenService tokenService,
    ISessionOperationalStore operationalStore,
    IOptions<SessionOptions> sessionOptions,
    TimeProvider timeProvider,
    AuthenticationMetrics metrics,
    ILogger<AuthenticationService> logger) : IAuthenticationService, ISessionValidator
{
    private const string Producer = "Sistema.Gestao.Empresarial.Api";
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> InProcessLocks = new();
    private readonly string _dummyHash = credentialHasher.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    private readonly SessionOptions _sessionOptions = sessionOptions.Value;

    public async Task<AuthenticationResponse?> LoginAsync(
        LoginRequest request,
        AuthOperationContext context,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var initialUser = await dbContext.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        var initialHash = initialUser?.SenhaHash ?? _dummyHash;
        var initiallyValid = credentialHasher.VerifyHashedPassword(initialHash, request.Password);

        if (initialUser is null)
        {
            await RegisterFailedLoginAsync(null, context, cancellationToken);
            metrics.LoginFailed();
            return null;
        }

        return await ExecuteWithStrategyAsync(async () =>
        {
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            await using var authenticationLock = await AcquireUserLockAsync(initialUser.Id, cancellationToken);

            var user = await dbContext.Usuarios.SingleAsync(x => x.Id == initialUser.Id, cancellationToken);
            var passwordValid = initiallyValid && credentialHasher.VerifyHashedPassword(user.SenhaHash, request.Password);
            var now = timeProvider.GetUtcNow();
            var temporarilyLocked = user.EstaTemporariamenteBloqueado(now);
            if (!passwordValid || !user.Ativo || temporarilyLocked)
            {
                if (!temporarilyLocked)
                {
                    user.RegistrarLoginInvalido(
                        now,
                        _sessionOptions.MaximumFailedLoginAttempts,
                        TimeSpan.FromMinutes(_sessionOptions.LockoutMinutes));
                }

                AddAuditAndOutbox("LoginFalhou", user.Guid, context, new { userGuid = user.Guid });
                await dbContext.SaveChangesAsync(cancellationToken);
                await CommitAsync(transaction, cancellationToken);
                metrics.LoginFailed();
                return null;
            }

            var previousSessions = await dbContext.UsuariosSessoes
                .Where(x => x.UsuarioId == user.Id && x.Ativo && !x.Revogado)
                .ToListAsync(cancellationToken);
            foreach (var previousSession in previousSessions)
            {
                previousSession.Revogar("NOVO_LOGIN", now);
            }

            user.RegistrarLoginValido(now);
            var sessionId = Guid.NewGuid();
            var token = tokenService.Create(user.Guid, sessionId, user.VersaoSessao, now);
            var session = new UsuarioSessao(
                Guid.NewGuid(),
                user.Id,
                sessionId,
                token.Jti,
                token.AccessTokenHash,
                token.RefreshTokenHash,
                user.VersaoSessao,
                now,
                now.AddDays(_sessionOptions.AbsoluteLifetimeDays),
                context.IpAddress,
                context.UserAgent);
            dbContext.UsuariosSessoes.Add(session);

            if (previousSessions.Count > 0)
            {
                AddAuditAndOutbox(
                    "UsuarioLogadoEmNovoDispositivo",
                    user.Guid,
                    context,
                    new { userGuid = user.Guid, sessionId });
            }
            AddAuditAndOutbox("LoginRealizado", user.Guid, context, new { userGuid = user.Guid, sessionId });

            await dbContext.SaveChangesAsync(cancellationToken);
            await operationalStore.ReplaceActiveSessionAsync(
                ToOperationalSession(user, session),
                previousSessions.Select(x => x.SessionId).ToArray(),
                cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            metrics.LoginSucceeded();
            metrics.SessionRevoked(previousSessions.Count);

            return ToResponse(token, sessionId, user.Guid, now);
        }, cancellationToken);
    }

    public async Task<AuthenticationResponse?> RefreshAsync(
        RefreshTokenRequest request,
        AuthOperationContext context,
        CancellationToken cancellationToken)
    {
        var presentedHash = tokenService.HashToken(request.RefreshToken);
        var initialSession = await dbContext.UsuariosSessoes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SessionId == request.SessionId, cancellationToken);
        if (initialSession is null)
        {
            metrics.RefreshFailed();
            return null;
        }

        return await ExecuteWithStrategyAsync(async () =>
        {
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            await using var authenticationLock = await AcquireUserLockAsync(initialSession.UsuarioId, cancellationToken);
            var session = await dbContext.UsuariosSessoes
                .Include(x => x.Usuario)
                .SingleAsync(x => x.Id == initialSession.Id, cancellationToken);
            var now = timeProvider.GetUtcNow();

            if (!TokenHashesEqual(session.RefreshTokenHash, presentedHash))
            {
                await RevokeAllSessionsAsync(session.Usuario, "REUTILIZACAO_REFRESH_TOKEN", context, now, cancellationToken);
                await CommitAsync(transaction, cancellationToken);
                metrics.RefreshFailed();
                return null;
            }

            if (!IsDurablyValid(session, session.Usuario, now, null))
            {
                if (session.Revogar("SESSAO_EXPIRADA", now))
                {
                    AddAuditAndOutbox("SessaoExpirada", session.Usuario.Guid, context, new { sessionId = session.SessionId });
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                await CommitAsync(transaction, cancellationToken);
                metrics.RefreshFailed();
                return null;
            }

            var token = tokenService.Create(session.Usuario.Guid, session.SessionId, session.VersaoSessao, now);
            session.RotacionarTokens(token.Jti, token.AccessTokenHash, token.RefreshTokenHash, now);
            AddAuditAndOutbox("RefreshRealizado", session.Usuario.Guid, context, new { sessionId = session.SessionId });
            await dbContext.SaveChangesAsync(cancellationToken);
            await operationalStore.RotateJtiAsync(session.SessionId, token.Jti, now, cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            metrics.RefreshSucceeded();
            return ToResponse(token, session.SessionId, session.Usuario.Guid, now);
        }, cancellationToken);
    }

    public async Task LogoutAsync(
        SessionTokenClaims claims,
        AuthOperationContext context,
        CancellationToken cancellationToken)
    {
        var initialUser = await dbContext.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Guid == claims.UserGuid, cancellationToken);
        if (initialUser is null)
        {
            return;
        }

        await ExecuteWithStrategyAsync(async () =>
        {
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            await using var authenticationLock = await AcquireUserLockAsync(initialUser.Id, cancellationToken);
            var user = await dbContext.Usuarios.SingleAsync(x => x.Id == initialUser.Id, cancellationToken);
            var sessions = await dbContext.UsuariosSessoes
                .Where(x => x.UsuarioId == user.Id && x.Ativo && !x.Revogado)
                .ToListAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var changed = false;
            foreach (var session in sessions)
            {
                changed |= session.Revogar("LOGOUT_MANUAL", now);
            }

            if (changed)
            {
                AddAuditAndOutbox("SessaoRevogada", user.Guid, context, new { motivo = "LOGOUT_MANUAL" });
                await dbContext.SaveChangesAsync(cancellationToken);
                metrics.SessionRevoked(sessions.Count);
                await operationalStore.RemoveAsync(user.Guid, sessions.Select(x => x.SessionId).ToArray(), cancellationToken);
            }
            await CommitAsync(transaction, cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<bool> ValidateAsync(SessionTokenClaims claims, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        try
        {
            var operationalResult = await operationalStore.ValidateAndTouchAsync(claims, now, cancellationToken);
            if (operationalResult == OperationalSessionValidation.Valid)
            {
                return true;
            }
            if (operationalResult == OperationalSessionValidation.CheckpointRequired)
            {
                await CheckpointActivityAsync(claims.SessionId, now, cancellationToken);
                return true;
            }
            if (operationalResult == OperationalSessionValidation.Invalid)
            {
                return false;
            }
            if (operationalResult == OperationalSessionValidation.Expired)
            {
                await RevokeExpiredSessionAsync(claims.SessionId, now, cancellationToken);
                return false;
            }
        }
        catch (SessionStoreUnavailableException exception) when (_sessionOptions.EnableSqlFallback)
        {
            logger.LogWarning(exception, "Fallback controlado Redis para SQL na validação da sessão {SessionId}", claims.SessionId);
            metrics.RedisFallback();
            return await ValidateFromSqlAsync(claims, now, rehydrate: false, cancellationToken);
        }
        catch (SessionStoreUnavailableException exception)
        {
            logger.LogError(exception, "Validação de sessão negada porque Redis está indisponível");
            return false;
        }

        return await ValidateFromSqlAsync(claims, now, rehydrate: true, cancellationToken);
    }

    private async Task<bool> ValidateFromSqlAsync(
        SessionTokenClaims claims,
        DateTimeOffset now,
        bool rehydrate,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.UsuariosSessoes.AsNoTracking()
            .Include(x => x.Usuario)
            .SingleOrDefaultAsync(x => x.SessionId == claims.SessionId, cancellationToken);
        if (session is null)
        {
            return false;
        }

        if (!IsDurablyValid(session, session.Usuario, now, null))
        {
            if (!session.Revogado && session.Ativo)
            {
                await RevokeExpiredSessionAsync(session.SessionId, now, cancellationToken);
            }
            return false;
        }

        if (claims.UserGuid != session.Usuario.Guid
            || claims.Jti != session.Jti
            || claims.SessionVersion != session.VersaoSessao)
        {
            return false;
        }

        if (rehydrate)
        {
            await operationalStore.UpsertAsync(ToOperationalSession(session.Usuario, session), cancellationToken);
        }
        return true;
    }

    private bool IsDurablyValid(
        UsuarioSessao session,
        Usuario user,
        DateTimeOffset now,
        SessionTokenClaims? claims)
    {
        return session.Ativo
            && !session.Revogado
            && user.Ativo
            && !user.EstaTemporariamenteBloqueado(now)
            && session.VersaoSessao == user.VersaoSessao
            && session.ExpiraEm > now
            && now - session.UltimaAtividadeEm < TimeSpan.FromMinutes(_sessionOptions.InactivityTimeoutMinutes)
            && (claims is null || (
                claims.UserGuid == user.Guid
                && claims.Jti == session.Jti
                && claims.SessionVersion == session.VersaoSessao));
    }

    private async Task CheckpointActivityAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await dbContext.UsuariosSessoes
            .Where(x => x.SessionId == sessionId && x.Ativo && !x.Revogado)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.UltimaAtividadeEm, now)
                .SetProperty(x => x.DataAtualizacao, now), cancellationToken);
    }

    private async Task RevokeExpiredSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var session = await dbContext.UsuariosSessoes.Include(x => x.Usuario)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
        if (session is null || !session.Revogar("SESSAO_EXPIRADA_INATIVIDADE", now))
        {
            return;
        }

        var context = new AuthOperationContext(Guid.NewGuid(), Activity.Current?.TraceId.ToString() ?? string.Empty, null, null);
        AddAuditAndOutbox("SessaoExpirada", session.Usuario.Guid, context, new { sessionId });
        await dbContext.SaveChangesAsync(cancellationToken);
        metrics.SessionExpired();
    }

    private async Task RevokeAllSessionsAsync(
        Usuario user,
        string reason,
        AuthOperationContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.UsuariosSessoes
            .Where(x => x.UsuarioId == user.Id && x.Ativo && !x.Revogado)
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0)
        {
            return;
        }

        foreach (var activeSession in sessions)
        {
            activeSession.Revogar(reason, now);
        }
        AddAuditAndOutbox("SessaoRevogada", user.Guid, context, new { motivo = reason });
        await dbContext.SaveChangesAsync(cancellationToken);
        await operationalStore.RemoveAsync(user.Guid, sessions.Select(x => x.SessionId).ToArray(), cancellationToken);
    }

    private async Task RegisterFailedLoginAsync(
        Guid? userGuid,
        AuthOperationContext context,
        CancellationToken cancellationToken)
    {
        AddAuditAndOutbox("LoginFalhou", userGuid, context, new { userGuid });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddAuditAndOutbox(string eventType, Guid? userGuid, AuthOperationContext context, object data)
    {
        var now = timeProvider.GetUtcNow();
        dbContext.AuditLogs.Add(new AuditLog(
            Guid.NewGuid(),
            "Autenticacao",
            userGuid,
            eventType,
            userGuid,
            now,
            context.CorrelationId,
            context.TraceId,
            context.IpAddress));

        var eventId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var envelope = new
        {
            eventId,
            messageId,
            eventType,
            eventVersion = 1,
            correlationId = context.CorrelationId,
            traceId = context.TraceId,
            occurredAt = now,
            producer = Producer,
            data
        };
        dbContext.OutboxMessages.Add(new OutboxMessage(
            Guid.NewGuid(), messageId, eventId, eventType, 1, JsonSerializer.Serialize(envelope),
            context.CorrelationId, context.TraceId, Producer, now));
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
    }

    private async Task<T> ExecuteWithStrategyAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref attempt) > 1)
            {
                dbContext.ChangeTracker.Clear();
            }

            return await operation();
        });
    }

    private async Task<IAsyncDisposable> AcquireUserLockAsync(long userId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            var semaphore = InProcessLocks.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            return new SemaphoreReleaser(semaphore);
        }

        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            SELECT @result;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = $"sge:auth:user:{userId.ToString(CultureInfo.InvariantCulture)}";
        command.Parameters.Add(parameter);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new TimeoutException("Não foi possível adquirir o lock de autenticação do usuário.");
        }
        return NoopAsyncDisposable.Instance;
    }

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    private static OperationalSession ToOperationalSession(Usuario user, UsuarioSessao session) => new(
        user.Guid,
        session.SessionId,
        session.Jti,
        session.VersaoSessao,
        session.DataCriacao,
        session.UltimaAtividadeEm,
        session.UltimaAtividadeEm);

    private AuthenticationResponse ToResponse(TokenMaterial token, Guid sessionId, Guid userGuid, DateTimeOffset now) => new(
        token.AccessToken,
        token.RefreshToken,
        "Bearer",
        Math.Max(1, (int)(token.AccessTokenExpiresAt - now).TotalSeconds),
        sessionId,
        userGuid);

    private static bool TokenHashesEqual(string expected, string actual)
    {
        var left = Encoding.ASCII.GetBytes(expected);
        var right = Encoding.ASCII.GetBytes(actual);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private sealed class SemaphoreReleaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoopAsyncDisposable Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
