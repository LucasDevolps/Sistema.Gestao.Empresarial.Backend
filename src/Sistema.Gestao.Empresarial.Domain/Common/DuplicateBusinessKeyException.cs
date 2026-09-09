namespace Sistema.Gestao.Empresarial.Domain.Common;

/// <summary>
/// Sinaliza a tentativa de criar ou atualizar um registro cuja chave natural de
/// negócio já pertence a outro registro (por exemplo, duas profissões com o mesmo
/// título, desconsiderando caixa e espaços das extremidades).
/// </summary>
/// <remarks>
/// É tratada pelo handler global de exceções como <c>HTTP 409 Conflict</c>, sempre
/// com o mesmo <see cref="ErrorCode"/> estável, independentemente de a duplicidade
/// ter sido detectada pela pré-checagem na aplicação ou pelo índice único do banco
/// sob concorrência. A <see cref="System.Exception.Message"/> é um texto fixo,
/// redigido para consumo do cliente: não contém o valor informado pelo usuário nem
/// detalhes de infraestrutura (SQL, número/nome de constraint, stack trace).
/// O handler global também <b>não</b> registra a cadeia de <c>InnerException</c>
/// desta exceção nos logs — apenas metadados seguros (code, field, número do erro
/// SQL quando conhecido). A <see cref="System.Exception.InnerException"/> permanece
/// preenchida para diagnóstico interno em outras camadas.
/// </remarks>
public sealed class DuplicateBusinessKeyException : Exception
{
    /// <summary>
    /// Código estável para o front-end interpretar o erro sem depender do texto da
    /// mensagem. Exposto na extensão <c>code</c> do <c>ProblemDetails</c>.
    /// </summary>
    public const string ErrorCode = "DUPLICATE_BUSINESS_KEY";

    public DuplicateBusinessKeyException(
        string message,
        string? field = null,
        Exception? innerException = null,
        int? sqlErrorNumber = null)
        : base(message, innerException)
    {
        Field = field;
        SqlErrorNumber = sqlErrorNumber;
    }

    /// <summary>
    /// Campo do <b>contrato público</b> da API em conflito (por exemplo, <c>"name"</c>
    /// ou <c>"email"</c>), ou <c>null</c> quando não é possível determiná-lo com
    /// segurança. Nunca corresponde a nome de coluna, tabela ou constraint interna.
    /// </summary>
    public string? Field { get; }

    /// <summary>
    /// Número do erro do SQL Server (<c>2601</c> índice único / <c>2627</c> constraint)
    /// quando a duplicidade foi detectada pelo banco sob concorrência; <c>null</c>
    /// quando detectada pela pré-checagem. Metadado seguro — não contém texto da
    /// mensagem do SQL Server, nome de índice/constraint nem o valor duplicado.
    /// </summary>
    public int? SqlErrorNumber { get; }
}
