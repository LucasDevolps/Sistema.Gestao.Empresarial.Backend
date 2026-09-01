using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Auditoria;

public sealed class ApiRequestLog : EntidadeAuditavel
{
    private ApiRequestLog() { }

    public ApiRequestLog(
        Guid guid,
        DateTimeOffset dataHoraInicio,
        DateTimeOffset dataHoraFim,
        string metodoHttp,
        string endpoint,
        string? queryString,
        string requestHeaders,
        string? requestBody,
        string responseHeaders,
        string? responseBody,
        int statusCode,
        long tempoExecucaoMs,
        bool sucesso,
        Guid correlationId,
        string traceId,
        string? ipOrigem,
        string? userAgent,
        long? usuarioId,
        Guid? usuarioGuid,
        string ambiente,
        string? exception) : base(guid, dataHoraInicio)
    {
        if (dataHoraFim < dataHoraInicio)
            throw new DomainException("O fim da requisição não pode anteceder o início.");

        DataHoraInicio = dataHoraInicio;
        DataHoraFim = dataHoraFim;
        MetodoHttp = Guard.TextoObrigatorio(metodoHttp, nameof(MetodoHttp), 16);
        Endpoint = Guard.TextoObrigatorio(endpoint, nameof(Endpoint), 2048);
        QueryString = queryString;
        RequestHeaders = Guard.TextoObrigatorio(requestHeaders, nameof(RequestHeaders), 262144);
        RequestBody = requestBody;
        ResponseHeaders = Guard.TextoObrigatorio(responseHeaders, nameof(ResponseHeaders), 262144);
        ResponseBody = responseBody;
        StatusCode = statusCode;
        TempoExecucaoMs = Math.Max(0, tempoExecucaoMs);
        Sucesso = sucesso;
        CorrelationId = correlationId;
        TraceId = Guard.TextoObrigatorio(traceId, nameof(TraceId), 64);
        IpOrigem = ipOrigem;
        UserAgent = userAgent;
        UsuarioId = usuarioId;
        UsuarioGuid = usuarioGuid;
        Ambiente = Guard.TextoObrigatorio(ambiente, nameof(Ambiente), 100);
        Exception = exception;
    }

    public DateTimeOffset DataHoraInicio { get; private set; }
    public DateTimeOffset DataHoraFim { get; private set; }
    public string MetodoHttp { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string? QueryString { get; private set; }
    public string RequestHeaders { get; private set; } = string.Empty;
    public string? RequestBody { get; private set; }
    public string ResponseHeaders { get; private set; } = string.Empty;
    public string? ResponseBody { get; private set; }
    public int StatusCode { get; private set; }
    public long TempoExecucaoMs { get; private set; }
    public bool Sucesso { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string TraceId { get; private set; } = string.Empty;
    public string? IpOrigem { get; private set; }
    public string? UserAgent { get; private set; }
    public long? UsuarioId { get; private set; }
    public Guid? UsuarioGuid { get; private set; }
    public string Ambiente { get; private set; } = string.Empty;
    public string? Exception { get; private set; }
}
