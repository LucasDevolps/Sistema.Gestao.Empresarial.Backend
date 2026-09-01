using System.Diagnostics.Metrics;

namespace Sistema.Gestao.Empresarial.Infrastructure.Observability;

public sealed class PermissionMetrics
{
    public const string MeterName = "Sistema.Gestao.Empresarial.Permissions";
    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _fallbacks;
    private readonly Counter<long> _barrierDenials;
    private readonly Counter<long> _invalidations;

    public PermissionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("permissions.cache.hit");
        _misses = meter.CreateCounter<long>("permissions.cache.miss");
        _fallbacks = meter.CreateCounter<long>("permissions.cache.sql_fallback");
        _barrierDenials = meter.CreateCounter<long>("permissions.cache.barrier_denied");
        _invalidations = meter.CreateCounter<long>("permissions.cache.invalidated");
    }

    public void Hit() => _hits.Add(1);
    public void Miss() => _misses.Add(1);
    public void Fallback() => _fallbacks.Add(1);
    public void BarrierDenied() => _barrierDenials.Add(1);
    public void Invalidated() => _invalidations.Add(1);
}
