using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Auditoria;

public sealed class AuditLog : EntidadeAuditavel
{
    private AuditLog() { }

    public AuditLog(
        Guid guid,
        string entidade,
        Guid? entidadeGuid,
        string acao,
        Guid? usuarioGuid,
        DateTimeOffset ocorridoEm,
        Guid correlationId,
        string traceId,
        string? ip,
        string? valorAnterior = null,
        string? valorNovo = null) : base(guid, ocorridoEm)
    {
        Entidade = Guard.TextoObrigatorio(entidade, nameof(Entidade), 150);
        EntidadeGuid = entidadeGuid;
        Acao = Guard.TextoObrigatorio(acao, nameof(Acao), 100);
        UsuarioGuid = usuarioGuid;
        DataHora = ocorridoEm;
        CorrelationId = correlationId;
        TraceId = traceId;
        Ip = ip;
        ValorAnterior = valorAnterior;
        ValorNovo = valorNovo;
    }

    public string Entidade { get; private set; } = string.Empty;
    public Guid? EntidadeGuid { get; private set; }
    public string Acao { get; private set; } = string.Empty;
    public string? ValorAnterior { get; private set; }
    public string? ValorNovo { get; private set; }
    public Guid? UsuarioGuid { get; private set; }
    public DateTimeOffset DataHora { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string TraceId { get; private set; } = string.Empty;
    public string? Ip { get; private set; }
}
