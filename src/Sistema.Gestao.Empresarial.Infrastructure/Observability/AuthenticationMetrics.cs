using System.Diagnostics.Metrics;

namespace Sistema.Gestao.Empresarial.Infrastructure.Observability;

public sealed class AuthenticationMetrics
{
    public const string MeterName = "Sistema.Gestao.Empresarial.Authentication";
    private readonly Counter<long> _loginSuccess;
    private readonly Counter<long> _loginFailed;
    private readonly Counter<long> _refreshSuccess;
    private readonly Counter<long> _refreshFailed;
    private readonly Counter<long> _sessionsRevoked;
    private readonly Counter<long> _sessionsExpired;
    private readonly Counter<long> _redisFallback;

    public AuthenticationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _loginSuccess = meter.CreateCounter<long>("auth.login.success");
        _loginFailed = meter.CreateCounter<long>("auth.login.failed");
        _refreshSuccess = meter.CreateCounter<long>("auth.refresh.success");
        _refreshFailed = meter.CreateCounter<long>("auth.refresh.failed");
        _sessionsRevoked = meter.CreateCounter<long>("auth.session.revoked");
        _sessionsExpired = meter.CreateCounter<long>("auth.session.expired");
        _redisFallback = meter.CreateCounter<long>("auth.redis.fallback");
    }

    public void LoginSucceeded() => _loginSuccess.Add(1);
    public void LoginFailed() => _loginFailed.Add(1);
    public void RefreshSucceeded() => _refreshSuccess.Add(1);
    public void RefreshFailed() => _refreshFailed.Add(1);
    public void SessionRevoked(long count = 1) => _sessionsRevoked.Add(count);
    public void SessionExpired() => _sessionsExpired.Add(1);
    public void RedisFallback() => _redisFallback.Add(1);
}
