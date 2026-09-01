using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sistema.Gestao.Empresarial.Infrastructure.Observability;

public sealed class OutboxMetrics
{
    public const string MeterName = "Sistema.Gestao.Empresarial.Outbox";
    public static readonly ActivitySource ActivitySource = new(MeterName);
    private readonly Counter<long> _claimed;
    private readonly Counter<long> _published;
    private readonly Counter<long> _transientFailures;
    private readonly Counter<long> _permanentFailures;
    private readonly Counter<long> _recoveredLeases;
    private readonly Histogram<double> _publishLatency;

    public OutboxMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _claimed = meter.CreateCounter<long>("sge.outbox.claimed");
        _published = meter.CreateCounter<long>("sge.outbox.published");
        _transientFailures = meter.CreateCounter<long>("sge.outbox.failures.transient");
        _permanentFailures = meter.CreateCounter<long>("sge.outbox.failures.permanent");
        _recoveredLeases = meter.CreateCounter<long>("sge.outbox.leases.recovered");
        _publishLatency = meter.CreateHistogram<double>("sge.outbox.publish.latency", "ms");
    }

    public void Claimed(int count) => _claimed.Add(count);
    public void Published(TimeSpan latency)
    {
        _published.Add(1);
        _publishLatency.Record(latency.TotalMilliseconds);
    }
    public void TransientFailure() => _transientFailures.Add(1);
    public void PermanentFailure() => _permanentFailures.Add(1);
    public void RecoveredLease() => _recoveredLeases.Add(1);
}
