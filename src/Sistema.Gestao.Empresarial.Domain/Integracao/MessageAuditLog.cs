using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Integracao;

public sealed class MessageAuditLog : EntidadeAuditavel
{
    private MessageAuditLog()
    {
    }

    public MessageAuditLog(
        Guid guid,
        Guid messageId,
        string eventType,
        string consumer,
        string status,
        int tentativa,
        Guid correlationId,
        string traceId,
        DateTimeOffset ocorridoEm,
        string? detalhe = null) : base(guid, ocorridoEm)
    {
        MessageId = messageId;
        EventType = Guard.TextoObrigatorio(eventType, nameof(EventType), 200);
        Consumer = Guard.TextoObrigatorio(consumer, nameof(Consumer), 200);
        Status = Guard.TextoObrigatorio(status, nameof(Status), 50);
        Tentativa = tentativa;
        CorrelationId = correlationId;
        TraceId = Guard.TextoObrigatorio(traceId, nameof(TraceId), 64);
        OcorridoEm = ocorridoEm;
        Detalhe = Limitar(detalhe);
    }

    public Guid MessageId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Consumer { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int Tentativa { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string TraceId { get; private set; } = string.Empty;
    public DateTimeOffset OcorridoEm { get; private set; }
    public string? Detalhe { get; private set; }

    private static string? Limitar(string? detalhe)
    {
        if (string.IsNullOrWhiteSpace(detalhe))
            return null;

        var normalized = detalhe.Trim();
        return normalized.Length <= 4000 ? normalized : normalized[..4000];
    }
}
