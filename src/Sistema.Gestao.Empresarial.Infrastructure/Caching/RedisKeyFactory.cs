using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;

namespace Sistema.Gestao.Empresarial.Infrastructure.Caching;

public interface IRedisKeyFactory
{
    string Session(Guid sessionId);
    string ActiveSession(Guid userGuid);
    string Permissions(Guid userGuid);
    string DirtySessionActivity();
}

public sealed class RedisKeyFactory(IOptions<RedisOptions> options, IHostEnvironment environment)
    : IRedisKeyFactory
{
    private readonly string _prefix = $"{options.Value.InstanceName}:{environment.EnvironmentName.ToLowerInvariant()}";

    public string Session(Guid sessionId) => $"{_prefix}:session:{sessionId:N}";

    public string ActiveSession(Guid userGuid) => $"{_prefix}:user:{userGuid:N}:active-session";

    public string Permissions(Guid userGuid) => $"{_prefix}:permissions:{userGuid:N}";

    public string DirtySessionActivity() => $"{_prefix}:session-activity:dirty";
}
