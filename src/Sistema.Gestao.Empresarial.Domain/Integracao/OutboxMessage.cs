using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Integracao;

public sealed class OutboxMessage : EntidadeAuditavel
{
    private OutboxMessage() { }

    public OutboxMessage(
        Guid guid,
        Guid messageId,
        Guid eventId,
        string eventType,
        int eventVersion,
        string payload,
        Guid correlationId,
        string traceId,
        string producer,
        DateTimeOffset ocorridoEm) : base(guid, ocorridoEm)
    {
        MessageId = messageId;
        EventId = eventId;
        EventType = Guard.TextoObrigatorio(eventType, nameof(EventType), 200);
        EventVersion = eventVersion;
        Payload = Guard.TextoObrigatorio(payload, nameof(Payload), int.MaxValue);
        CorrelationId = correlationId;
        TraceId = traceId;
        Producer = Guard.TextoObrigatorio(producer, nameof(Producer), 200);
        OccurredAt = ocorridoEm;
        Status = "PENDENTE";
    }

    public Guid MessageId { get; private set; }
    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public int EventVersion { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public string TraceId { get; private set; } = string.Empty;
    public string Producer { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int Tentativas { get; private set; }
    public DateTimeOffset? PublicadoEm { get; private set; }
    public DateTimeOffset? UltimaTentativaEm { get; private set; }
    public DateTimeOffset? ProximaTentativaEm { get; private set; }
    public DateTimeOffset? BloqueadoAte { get; private set; }
    public Guid? LockId { get; private set; }
    public string? WorkerId { get; private set; }
    public string? Erro { get; private set; }

    public void Reivindicar(Guid lockId, string workerId, DateTimeOffset agora, TimeSpan lease)
    {
        if (lockId == Guid.Empty)
            throw new DomainException("O identificador do claim da Outbox é obrigatório.");
        if (!PodeSerReivindicada(agora))
            throw new DomainException("A mensagem da Outbox ainda não pode ser reivindicada.");

        LockId = lockId;
        WorkerId = Guard.TextoObrigatorio(workerId, nameof(WorkerId), 200);
        BloqueadoAte = agora.Add(lease);
        UltimaTentativaEm = agora;
        Tentativas++;
        Status = "PROCESSANDO";
        MarcarAtualizacao(agora);
    }

    public void MarcarPublicada(Guid lockId, DateTimeOffset agora)
    {
        ValidarLock(lockId);
        Status = "PUBLICADA";
        PublicadoEm = agora;
        ProximaTentativaEm = null;
        Erro = null;
        LimparLock();
        MarcarAtualizacao(agora);
    }

    public void RegistrarFalhaTransitoria(
        Guid lockId,
        string erro,
        DateTimeOffset agora,
        DateTimeOffset proximaTentativa)
    {
        ValidarLock(lockId);
        Status = "ERRO";
        Erro = LimitarErro(erro);
        ProximaTentativaEm = proximaTentativa;
        LimparLock();
        MarcarAtualizacao(agora);
    }

    public void RegistrarFalhaPermanente(Guid lockId, string erro, DateTimeOffset agora)
    {
        ValidarLock(lockId);
        Status = "ERRO_PERMANENTE";
        Erro = LimitarErro(erro);
        ProximaTentativaEm = null;
        LimparLock();
        MarcarAtualizacao(agora);
    }

    public bool PodeSerReivindicada(DateTimeOffset agora) =>
        (Status == "PENDENTE" && (!ProximaTentativaEm.HasValue || ProximaTentativaEm <= agora))
        || (Status == "ERRO" && ProximaTentativaEm <= agora)
        || (Status == "PROCESSANDO" && BloqueadoAte <= agora);

    private void ValidarLock(Guid lockId)
    {
        if (Status != "PROCESSANDO" || LockId != lockId)
            throw new DomainException("O claim da mensagem da Outbox não é mais válido.");
    }

    private void LimparLock()
    {
        LockId = null;
        WorkerId = null;
        BloqueadoAte = null;
    }

    private static string LimitarErro(string erro)
    {
        var normalized = string.IsNullOrWhiteSpace(erro) ? "Erro técnico não detalhado." : erro.Trim();
        return normalized.Length <= 4000 ? normalized : normalized[..4000];
    }
}
