using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Integracao;

public sealed class InboxMessage : EntidadeAuditavel
{
    private InboxMessage()
    {
    }

    public InboxMessage(
        Guid guid,
        Guid messageId,
        string consumer,
        string eventType,
        string payload,
        Guid correlationId,
        string traceId,
        DateTimeOffset recebidoEm) : base(guid, recebidoEm)
    {
        if (messageId == Guid.Empty)
            throw new DomainException("O MessageId da Inbox é obrigatório.");

        MessageId = messageId;
        Consumer = Guard.TextoObrigatorio(consumer, nameof(Consumer), 200);
        EventType = Guard.TextoObrigatorio(eventType, nameof(EventType), 200);
        Payload = Guard.TextoObrigatorio(payload, nameof(Payload), int.MaxValue);
        CorrelationId = correlationId;
        TraceId = Guard.TextoObrigatorio(traceId, nameof(TraceId), 64);
        RecebidoEm = recebidoEm;
        Status = InboxStatus.Recebida;
    }

    public Guid MessageId { get; private set; }
    public string Consumer { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public string TraceId { get; private set; } = string.Empty;
    public DateTimeOffset RecebidoEm { get; private set; }
    public DateTimeOffset? ProcessadoEm { get; private set; }
    public DateTimeOffset? UltimaTentativaEm { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int Tentativas { get; private set; }
    public string? Resultado { get; private set; }
    public string? Erro { get; private set; }

    public bool Finalizada => Status is InboxStatus.Processada
        or InboxStatus.RejeitadaRegraNegocio
        or InboxStatus.RejeitadaValidacao
        or InboxStatus.Dlq;

    public void IniciarTentativa(DateTimeOffset agora)
    {
        if (Finalizada)
            throw new DomainException("Uma mensagem finalizada da Inbox não pode ser reprocessada automaticamente.");

        Tentativas++;
        UltimaTentativaEm = agora;
        Status = InboxStatus.Processando;
        Erro = null;
        MarcarAtualizacao(agora);
    }

    public void MarcarProcessada(DateTimeOffset agora, string? resultado = null)
    {
        ValidarProcessando();
        Status = InboxStatus.Processada;
        ProcessadoEm = agora;
        Resultado = Limitar(resultado, 2000);
        Erro = null;
        MarcarAtualizacao(agora);
    }

    public void MarcarRejeitada(string status, string motivo, DateTimeOffset agora)
    {
        ValidarProcessando();
        if (status is not InboxStatus.RejeitadaRegraNegocio and not InboxStatus.RejeitadaValidacao)
            throw new DomainException("Status de rejeição da Inbox inválido.");

        Status = status;
        ProcessadoEm = agora;
        Resultado = Limitar(motivo, 2000);
        Erro = null;
        MarcarAtualizacao(agora);
    }

    public void RegistrarFalhaTecnica(string erro, bool enviarDlq, DateTimeOffset agora)
    {
        if (Finalizada)
            return;

        if (Status != InboxStatus.Processando)
            IniciarTentativa(agora);

        Status = enviarDlq ? InboxStatus.Dlq : InboxStatus.Erro;
        Erro = Limitar(erro, 4000);
        ProcessadoEm = enviarDlq ? agora : null;
        MarcarAtualizacao(agora);
    }

    private void ValidarProcessando()
    {
        if (Status != InboxStatus.Processando)
            throw new DomainException("A mensagem da Inbox não possui uma tentativa em processamento.");
    }

    private static string? Limitar(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.Length <= length ? normalized : normalized[..length];
    }
}

public static class InboxStatus
{
    public const string Recebida = "RECEBIDA";
    public const string Processando = "PROCESSANDO";
    public const string Processada = "PROCESSADA";
    public const string RejeitadaRegraNegocio = "REJEITADA_REGRA_NEGOCIO";
    public const string RejeitadaValidacao = "REJEITADA_VALIDACAO";
    public const string Erro = "ERRO";
    public const string Dlq = "DLQ";
}
