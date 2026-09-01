using System.Diagnostics.Metrics;

namespace Sistema.Gestao.Empresarial.Infrastructure.Observability;

public sealed class InboxMetrics
{
    public const string MeterName = "Sistema.Gestao.Empresarial.Inbox";
    private readonly Counter<long> _received;
    private readonly Counter<long> _processed;
    private readonly Counter<long> _duplicates;
    private readonly Counter<long> _rejected;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _dlq;

    public InboxMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _received = meter.CreateCounter<long>("sge.inbox.received");
        _processed = meter.CreateCounter<long>("sge.inbox.processed");
        _duplicates = meter.CreateCounter<long>("sge.inbox.duplicates");
        _rejected = meter.CreateCounter<long>("sge.inbox.rejected");
        _retries = meter.CreateCounter<long>("sge.inbox.retries");
        _dlq = meter.CreateCounter<long>("sge.inbox.dlq");
    }

    public void Received() => _received.Add(1);
    public void Processed() => _processed.Add(1);
    public void Duplicate() => _duplicates.Add(1);
    public void Rejected(string reason) => _rejected.Add(1, new KeyValuePair<string, object?>("reason", reason));
    public void Retry() => _retries.Add(1);
    public void SentToDlq() => _dlq.Add(1);
}
