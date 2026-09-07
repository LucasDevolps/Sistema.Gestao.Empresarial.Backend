using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.Organizations;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/unidades-hospitalares")]
public sealed class HospitalUnitsController(
    IOrganizationCatalogService organizations,
    IValidator<OrganizationCatalogListQuery> listValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewEmployees)]
    [ProducesResponseType<HospitalUnitPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new OrganizationCatalogListQuery(search, active, page, pageSize);
        var validation = await listValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryGetActorGuid(out var actorGuid))
        {
            return Unauthorized();
        }

        return Ok(await organizations.ListHospitalUnitsAsync(actorGuid, query, cancellationToken));
    }

    [HttpGet("{unitGuid:guid}")]
    [RequirePermission(PermissionCodes.ViewEmployees)]
    [ProducesResponseType<HospitalUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByGuid(Guid unitGuid, CancellationToken cancellationToken)
    {
        if (!TryGetActorGuid(out var actorGuid))
        {
            return Unauthorized();
        }

        var response = await organizations.GetHospitalUnitAsync(actorGuid, unitGuid, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private bool TryGetActorGuid(out Guid actorGuid) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out actorGuid);

    private static ValidationProblemDetails ToProblem(ValidationResult validation) =>
        new(validation.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).ToArray()));
}
