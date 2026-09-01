using System.Text.Json;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Infrastructure.Caching;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using StackExchange.Redis;

namespace Sistema.Gestao.Empresarial.Infrastructure.Authorization;

public sealed record PermissionCacheEntry(long Version, bool Ready, string[] Permissions);

public interface IPermissionCache
{
    Task<PermissionCacheEntry?> GetAsync(Guid userGuid, CancellationToken cancellationToken);
    Task<bool> PublishAsync(Guid userGuid, PermissionCacheEntry entry, CancellationToken cancellationToken);
    Task AdvanceVersionAsync(Guid userGuid, long version, CancellationToken cancellationToken);
}

public sealed class PermissionCache(
    IConnectionMultiplexer connection,
    IRedisKeyFactory keys,
    IOptions<CacheOptions> options) : IPermissionCache
{
    private const string PublishScript = """
        local currentJson = redis.call('GET', KEYS[1])
        if currentJson then
            local current = cjson.decode(currentJson)
            if tonumber(current.Version) > tonumber(ARGV[1]) then return 0 end
        end
        redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
        return 1
        """;

    private const string AdvanceScript = """
        local currentJson = redis.call('GET', KEYS[1])
        if currentJson then
            local current = cjson.decode(currentJson)
            if tonumber(current.Version) > tonumber(ARGV[1]) then return -1 end
        end
        redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
        return 1
        """;

    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(options.Value.PermissionTtlSeconds);

    public async Task<PermissionCacheEntry?> GetAsync(Guid userGuid, CancellationToken cancellationToken)
    {
        try
        {
            var value = await connection.GetDatabase().StringGetAsync(keys.Permissions(userGuid));
            return value.HasValue ? JsonSerializer.Deserialize<PermissionCacheEntry>(value.ToString()) : null;
        }
        catch (RedisException exception)
        {
            throw new PermissionCacheUnavailableException("Redis de permissões está indisponível.", exception);
        }
    }

    public async Task<bool> PublishAsync(Guid userGuid, PermissionCacheEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var result = (long)await connection.GetDatabase().ScriptEvaluateAsync(
                PublishScript,
                [keys.Permissions(userGuid)],
                [entry.Version, JsonSerializer.Serialize(entry), (long)_ttl.TotalMilliseconds]);
            return result == 1;
        }
        catch (RedisException exception)
        {
            throw new PermissionCacheUnavailableException("Redis de permissões está indisponível.", exception);
        }
    }

    public async Task AdvanceVersionAsync(Guid userGuid, long version, CancellationToken cancellationToken)
    {
        var barrier = new PermissionCacheEntry(version, false, []);
        try
        {
            var result = (long)await connection.GetDatabase().ScriptEvaluateAsync(
                AdvanceScript,
                [keys.Permissions(userGuid)],
                [version, JsonSerializer.Serialize(barrier), (long)_ttl.TotalMilliseconds]);
            if (result < 0)
            {
                throw new InvalidOperationException("O cache possui uma versão de permissões mais recente.");
            }
        }
        catch (RedisException exception)
        {
            throw new PermissionCacheUnavailableException("Redis de permissões está indisponível.", exception);
        }
    }
}

public sealed class PermissionCacheUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
