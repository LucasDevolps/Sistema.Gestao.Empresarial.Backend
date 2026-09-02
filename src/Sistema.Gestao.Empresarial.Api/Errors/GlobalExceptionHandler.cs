using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Infrastructure.Authorization;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using Sistema.Gestao.Empresarial.Infrastructure.ProfessionalCatalogs;
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
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Operação não permitida.", LogLevel.Warning),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito de concorrência.", LogLevel.Warning),
            EmployeePersistenceConflictException => (StatusCodes.Status409Conflict, "Conflito de persistência.", LogLevel.Warning),
            ProfessionalCatalogPersistenceConflictException => (StatusCodes.Status409Conflict, "Conflito de persistência.", LogLevel.Warning),
            SessionStoreUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Serviço de sessão temporariamente indisponível.", LogLevel.Error),
            PermissionCacheUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Serviço de autorização temporariamente indisponível.", LogLevel.Error),
            TimeoutException => (StatusCodes.Status503ServiceUnavailable, "Operação temporariamente indisponível.", LogLevel.Error),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno.", LogLevel.Error)
        };

        logger.Log(level, exception, "Falha tratada na requisição {TraceIdentifier}", httpContext.TraceIdentifier);
        httpContext.Response.StatusCode = status;
        var correlationId = httpContext.Items.TryGetValue("CorrelationId", out var value) ? value?.ToString() : null;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { ["correlationId"] = correlationId }
            },
            Exception = exception
        });
    }
}
