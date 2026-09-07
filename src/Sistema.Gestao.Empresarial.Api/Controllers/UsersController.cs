using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.Identity;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
public sealed class UsersController(
    IPermissionAdministrationService permissions,
    IIdentityQueryService identityQueries,
    IValidator<IdentityListQuery> listValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ManageUserPermissions)]
    [ProducesResponseType<UserPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new IdentityListQuery(search, active, page, pageSize);
        var validation = await listValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorGuid))
        {
            return Unauthorized();
        }

        return Ok(await identityQueries.ListUsersAsync(actorGuid, query, cancellationToken));
    }

    [HttpGet("{userGuid:guid}/permissions")]
    [RequirePermission(PermissionCodes.ManageUserPermissions)]
    [ProducesResponseType<UserPermissionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermissions(Guid userGuid, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorGuid))
        {
            return Unauthorized();
        }

        var response = await permissions.GetAsync(actorGuid, userGuid, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("{userGuid:guid}/permissions/{permissionCode}")]
    [RequirePermission(PermissionCodes.ManageUserPermissions)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPermission(
        Guid userGuid,
        string permissionCode,
        SetUserPermissionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorGuid))
        {
            return Unauthorized();
        }

        var result = await permissions.SetDirectPermissionAsync(
            actorGuid,
            userGuid,
            permissionCode,
            request.Granted,
            GetCorrelationId(),
            Activity.Current?.TraceId.ToString() ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return result switch
        {
            PermissionChangeResult.Changed => NoContent(),
            PermissionChangeResult.Unchanged => NoContent(),
            PermissionChangeResult.Forbidden => Forbid(),
            PermissionChangeResult.UserNotFound => NotFound(),
            PermissionChangeResult.PermissionNotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private Guid GetCorrelationId() =>
        HttpContext.Items.TryGetValue("CorrelationId", out var value) && value is Guid guid
            ? guid
            : Guid.NewGuid();

    private static ValidationProblemDetails ToProblem(ValidationResult validation) =>
        new(validation.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).ToArray()));
}
