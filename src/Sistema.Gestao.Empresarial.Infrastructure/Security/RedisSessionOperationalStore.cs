using System.Text.Json;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Infrastructure.Caching;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using StackExchange.Redis;

namespace Sistema.Gestao.Empresarial.Infrastructure.Security;

public sealed class RedisSessionOperationalStore(
    IConnectionMultiplexer connection,
    IRedisKeyFactory keys,
    IOptions<SessionOptions> options) : ISessionOperationalStore
{
    private const string ReplaceScript = """
        for index = 3, #KEYS do redis.call('DEL', KEYS[index]) end
        redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[3])
        redis.call('SET', KEYS[2], ARGV[2], 'PX', ARGV[3])
        return 1
        """;

    private const string ValidateScript = """
        local json = redis.call('GET', KEYS[1])
        if not json then return 0 end
        local active = redis.call('GET', KEYS[2])
        if not active or active ~= ARGV[1] then return -1 end
        local state = cjson.decode(json)
        if state.UserGuid ~= ARGV[2] or state.SessionId ~= ARGV[1]
           or state.Jti ~= ARGV[3] or tostring(state.SessionVersion) ~= ARGV[4] then return -1 end
        local now = tonumber(ARGV[5])
        local timeout = tonumber(ARGV[6])
        if now - tonumber(state.LastActivityUnixMs) >= timeout then
            redis.call('DEL', KEYS[1])
            redis.call('DEL', KEYS[2])
            return -2
        end
        state.LastActivityUnixMs = now
        local checkpoint = 1
        if now - tonumber(state.LastPersistedUnixMs) >= tonumber(ARGV[7]) then
            state.LastPersistedUnixMs = now
            checkpoint = 2
        end
        redis.call('SET', KEYS[1], cjson.encode(state), 'PX', timeout)
        redis.call('PEXPIRE', KEYS[2], timeout)
        return checkpoint
        """;

    private const string RotateScript = """
        local json = redis.call('GET', KEYS[1])
        if not json then return 0 end
        local state = cjson.decode(json)
        state.Jti = ARGV[1]
        state.LastActivityUnixMs = tonumber(ARGV[2])
        redis.call('SET', KEYS[1], cjson.encode(state), 'PX', ARGV[3])
        redis.call('PEXPIRE', KEYS[2], ARGV[3])
        return 1
        """;

    private readonly TimeSpan _timeout = TimeSpan.FromMinutes(options.Value.InactivityTimeoutMinutes);
    private readonly TimeSpan _checkpointInterval = TimeSpan.FromSeconds(options.Value.ActivityPersistenceIntervalSeconds);

    public async Task ReplaceActiveSessionAsync(
        OperationalSession session,
        IReadOnlyCollection<Guid> previousSessionIds,
        CancellationToken cancellationToken)
    {
        var redisKeys = new List<RedisKey>
        {
            keys.Session(session.SessionId),
            keys.ActiveSession(session.UserGuid)
        };
        redisKeys.AddRange(previousSessionIds.Where(x => x != session.SessionId).Select(x => (RedisKey)keys.Session(x)));

        await ExecuteAsync(async database =>
        {
            await database.ScriptEvaluateAsync(
                ReplaceScript,
                [.. redisKeys],
                [Serialize(session), session.SessionId.ToString("N"), (long)_timeout.TotalMilliseconds]);
        });
    }

    public async Task<OperationalSessionValidation> ValidateAndTouchAsync(
        SessionTokenClaims claims,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async database =>
        {
            var result = (long)await database.ScriptEvaluateAsync(
                ValidateScript,
                [keys.Session(claims.SessionId), keys.ActiveSession(claims.UserGuid)],
                [
                    claims.SessionId.ToString("N"),
                    claims.UserGuid.ToString("N"),
                    claims.Jti,
                    claims.SessionVersion,
                    now.ToUnixTimeMilliseconds(),
                    (long)_timeout.TotalMilliseconds,
                    (long)_checkpointInterval.TotalMilliseconds
                ]);
            return result switch
            {
                2 => OperationalSessionValidation.CheckpointRequired,
                1 => OperationalSessionValidation.Valid,
                0 => OperationalSessionValidation.Missing,
                -2 => OperationalSessionValidation.Expired,
                _ => OperationalSessionValidation.Invalid
            };
        });
    }

    public async Task UpsertAsync(OperationalSession session, CancellationToken cancellationToken)
    {
        await ReplaceActiveSessionAsync(session, [], cancellationToken);
    }

    public async Task RotateJtiAsync(Guid sessionId, string jti, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await ExecuteAsync(async database =>
        {
            var json = await database.StringGetAsync(keys.Session(sessionId));
            if (!json.HasValue)
            {
                return;
            }

            var state = JsonSerializer.Deserialize<RedisSessionState>(json.ToString());
            if (state is null)
            {
                return;
            }

            await database.ScriptEvaluateAsync(
                RotateScript,
                [keys.Session(sessionId), keys.ActiveSession(Guid.ParseExact(state.UserGuid, "N"))],
                [jti, now.ToUnixTimeMilliseconds(), (long)_timeout.TotalMilliseconds]);
        });
    }

    public async Task RemoveAsync(Guid userGuid, IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken)
    {
        await ExecuteAsync(async database =>
        {
            var redisKeys = sessionIds.Select(x => (RedisKey)keys.Session(x)).ToList();
            redisKeys.Add(keys.ActiveSession(userGuid));
            if (redisKeys.Count > 0)
            {
                await database.KeyDeleteAsync([.. redisKeys]);
            }
        });
    }

    private async Task ExecuteAsync(Func<IDatabase, Task> action)
    {
        try
        {
            await action(connection.GetDatabase());
        }
        catch (RedisException exception)
        {
            throw new SessionStoreUnavailableException("Redis de sessões está indisponível.", exception);
        }
    }

    private async Task<T> ExecuteAsync<T>(Func<IDatabase, Task<T>> action)
    {
        try
        {
            return await action(connection.GetDatabase());
        }
        catch (RedisException exception)
        {
            throw new SessionStoreUnavailableException("Redis de sessões está indisponível.", exception);
        }
    }

    private static string Serialize(OperationalSession session) => JsonSerializer.Serialize(new RedisSessionState(
        session.UserGuid.ToString("N"),
        session.SessionId.ToString("N"),
        session.Jti,
        session.SessionVersion,
        session.CreatedAt.ToUnixTimeMilliseconds(),
        session.LastActivityAt.ToUnixTimeMilliseconds(),
        session.LastPersistedAt.ToUnixTimeMilliseconds()));

    private sealed record RedisSessionState(
        string UserGuid,
        string SessionId,
        string Jti,
        long SessionVersion,
        long CreatedAtUnixMs,
        long LastActivityUnixMs,
        long LastPersistedUnixMs);
}
