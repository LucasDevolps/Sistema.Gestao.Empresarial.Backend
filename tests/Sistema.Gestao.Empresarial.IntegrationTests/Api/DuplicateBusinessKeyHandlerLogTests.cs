using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Sistema.Gestao.Empresarial.Api.Errors;
using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

/// <summary>
/// Testa diretamente o <see cref="GlobalExceptionHandler"/>: quando a
/// <see cref="DuplicateBusinessKeyException"/> vem do caminho de concorrência (com
/// <c>InnerException</c> carregando texto sensível do SQL Server), nada desse texto
/// pode chegar ao <see cref="ILogger"/>; apenas metadados seguros.
/// </summary>
public sealed class DuplicateBusinessKeyHandlerLogTests
{
    [Fact]
    public async Task ConflitoDeConcorrencia_NaoLogaCadeiaSqlNemValorDuplicado_MasMantemDiagnostico()
    {
        const string sensivel = "sensitive-user@hospital.test";
        var logger = new CapturingLogger();
        var handler = new GlobalExceptionHandler(new NoopProblemDetailsService(), logger);
        var httpContext = new DefaultHttpContext { TraceIdentifier = "TRACE-123" };
        var correlationId = Guid.NewGuid();
        httpContext.Items["CorrelationId"] = correlationId;

        // Simula o que EF Core/SQL Server entregam numa violação de índice único: a
        // mensagem interna contém o valor duplicado (aqui, um e-mail).
        var innerSql = new InvalidOperationException(
            $"Cannot insert duplicate key row in object 'sge.Funcionarios' with unique index "
            + $"'IX_Funcionarios_Email'. The duplicate key value is ({sensivel}).");
        var exception = new DuplicateBusinessKeyException(
            "Já existe um registro com esta chave de negócio.",
            field: "name",
            innerException: new InvalidOperationException("An error occurred while saving the entity changes.", innerSql),
            sqlErrorNumber: 2601);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);

        var todoTexto = string.Join(
            "\n",
            logger.Entries.Select(e => $"{e.Level}|{e.Message}|{e.State}|{e.Exception}"));

        // Nada sensível vazou para o log — nem valor, nem mensagem SQL, nem nome de índice.
        Assert.DoesNotContain(sensivel, todoTexto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IX_Funcionarios_Email", todoTexto, StringComparison.Ordinal);
        Assert.DoesNotContain("duplicate key", todoTexto, StringComparison.OrdinalIgnoreCase);

        // O objeto de exceção não foi entregue ao logger (nenhuma entrada com Exception).
        Assert.All(logger.Entries, e => Assert.Null(e.Exception));

        // Diagnóstico correlacionável preservado.
        var diagnostico = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(DuplicateBusinessKeyException.ErrorCode, diagnostico.Message, StringComparison.Ordinal);
        Assert.Contains("Field=name", diagnostico.Message, StringComparison.Ordinal);
        Assert.Contains("SqlErrorNumber=2601", diagnostico.Message, StringComparison.Ordinal);
        Assert.Contains("TRACE-123", diagnostico.Message, StringComparison.Ordinal);
        Assert.Contains(correlationId.ToString(), diagnostico.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExcecaoInesperada_ContinuaLogandoComObjetoDeExcecao()
    {
        var logger = new CapturingLogger();
        var handler = new GlobalExceptionHandler(new NoopProblemDetailsService(), logger);
        var httpContext = new DefaultHttpContext { TraceIdentifier = "TRACE-999" };

        await handler.TryHandleAsync(
            httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        // Comportamento inalterado para os demais tipos: exceção entregue ao logger.
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception is not null);
    }

    private sealed record LogEntry(LogLevel Level, string Message, string? State, string? Exception);

    private sealed class CapturingLogger : ILogger<GlobalExceptionHandler>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(
                logLevel, formatter(state, exception), state?.ToString(), exception?.ToString()));
    }

    private sealed class NoopProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context) => ValueTask.CompletedTask;

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) => ValueTask.FromResult(true);
    }
}
