using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Infrastructure.Authorization;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using Sistema.Gestao.Empresarial.Api.Auditing;

namespace Sistema.Gestao.Empresarial.Api.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        httpContext.Items[ApiRequestAuditMiddleware.ExceptionTypeItem] =
            exception.GetType().FullName ?? exception.GetType().Name;
        var (status, title, level) = exception switch
        {
            DuplicateBusinessKeyException => (StatusCodes.Status409Conflict, "Registro duplicado.", LogLevel.Warning),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Operação não permitida.", LogLevel.Warning),
            OrganizationAccessDeniedException => (StatusCodes.Status403Forbidden, "Acesso organizacional negado.", LogLevel.Warning),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito de concorrência.", LogLevel.Warning),
            EmployeePersistenceConflictException => (StatusCodes.Status409Conflict, "Conflito de persistência.", LogLevel.Warning),
            SessionStoreUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Serviço de sessão temporariamente indisponível.", LogLevel.Error),
            PermissionCacheUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Serviço de autorização temporariamente indisponível.", LogLevel.Error),
            TimeoutException => (StatusCodes.Status503ServiceUnavailable, "Operação temporariamente indisponível.", LogLevel.Error),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno.", LogLevel.Error)
        };

        var correlationId = httpContext.Items.TryGetValue("CorrelationId", out var value) ? value?.ToString() : null;
        httpContext.Response.StatusCode = status;
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Extensions = { ["correlationId"] = correlationId }
        };

        if (exception is DuplicateBusinessKeyException duplicate)
        {
            // Conflito de chave de negócio é um erro esperado. A cadeia de exceções pode
            // carregar a mensagem do SqlException (com o valor duplicado / e-mail), então
            // NÃO passamos o objeto de exceção ao logger — apenas metadados seguros.
            logger.LogWarning(
                "Conflito de chave de negócio na requisição {TraceIdentifier}. "
                + "Code={Code} Field={Field} ExceptionType={ExceptionType} SqlErrorNumber={SqlErrorNumber} CorrelationId={CorrelationId}",
                httpContext.TraceIdentifier,
                DuplicateBusinessKeyException.ErrorCode,
                duplicate.Field ?? "(desconhecido)",
                nameof(DuplicateBusinessKeyException),
                duplicate.SqlErrorNumber?.ToString() ?? "(pré-checagem)",
                correlationId);

            // Mensagem fixa, redigida no domínio e segura para o cliente (sem o valor
            // informado nem detalhes de infraestrutura). O `code` estável permite ao
            // front-end tratar o erro sem depender do texto; o `field` usa apenas nomes
            // do contrato público da API.
            problemDetails.Detail = duplicate.Message;
            problemDetails.Extensions["code"] = DuplicateBusinessKeyException.ErrorCode;
            if (duplicate.Field is { Length: > 0 } field)
            {
                problemDetails.Extensions["field"] = field;
            }
        }
        else
        {
            logger.Log(level, exception, "Falha tratada na requisição {TraceIdentifier}", httpContext.TraceIdentifier);
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }
}
