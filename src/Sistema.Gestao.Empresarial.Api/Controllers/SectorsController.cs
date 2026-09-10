using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.Organizations;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/setores")]
public sealed class SectorsController(
    IOrganizationCatalogService organizations,
    IValidator<SectorListQuery> listValidator,
    IValidator<CreateSectorRequest> createValidator,
    IValidator<UpdateSectorRequest> updateValidator,
    IValidator<AddSectorServedUnitRequest> servedUnitValidator,
    IValidator<EndSectorServedUnitRequest> endServedUnitValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewSectors)]
    [ProducesResponseType<SectorPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] Guid? unitGuid,
        [FromQuery] Guid? categoryGuid,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new SectorListQuery(search, active, unitGuid, categoryGuid, page, pageSize);
        var validation = await listValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryGetActorGuid(out var actorGuid))
        {
            return Unauthorized();
        }

        return Ok(await organizations.ListSectorsAsync(actorGuid, query, cancellationToken));
    }

    [HttpGet("{sectorGuid:guid}", Name = nameof(GetSectorByGuid))]
    [RequirePermission(PermissionCodes.ViewSectors)]
    [ProducesResponseType<SectorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSectorByGuid(Guid sectorGuid, CancellationToken cancellationToken)
    {
        if (!TryGetActorGuid(out var actorGuid))
        {
            return Unauthorized();
        }

        var response = await organizations.GetSectorAsync(actorGuid, sectorGuid, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CreateSectors)]
    [ProducesResponseType<SectorResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        CreateSectorRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await organizations.CreateSectorAsync(request, context, cancellationToken);
        return CreatedAtRoute(nameof(GetSectorByGuid), new { sectorGuid = response.Guid }, response);
    }

    [HttpPut("{sectorGuid:guid}")]
    [RequirePermission(PermissionCodes.EditSectors)]
    [ProducesResponseType<SectorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid sectorGuid,
        UpdateSectorRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await organizations.UpdateSectorAsync(sectorGuid, request, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPatch("{sectorGuid:guid}/status")]
    [RequirePermission(PermissionCodes.EditSectors)]
    [ProducesResponseType<SectorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(
        Guid sectorGuid,
        ChangeSectorStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await organizations.ChangeSectorStatusAsync(
            sectorGuid, request.Active, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{sectorGuid:guid}/unidades-atendidas")]
    [RequirePermission(PermissionCodes.EditSectors)]
    [ProducesResponseType<SectorServedUnitResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddServedUnit(
        Guid sectorGuid,
        AddSectorServedUnitRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await servedUnitValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await organizations.AddSectorServedUnitAsync(
            sectorGuid, request, context, cancellationToken);
        return response is null
            ? NotFound()
            : Created($"/api/setores/{sectorGuid}/unidades-atendidas/{response.Guid}", response);
    }

    [HttpPost("{sectorGuid:guid}/unidades-atendidas/{relationshipGuid:guid}/encerrar")]
    [RequirePermission(PermissionCodes.EditSectors)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EndServedUnit(
        Guid sectorGuid,
        Guid relationshipGuid,
        EndSectorServedUnitRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await endServedUnitValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var result = await organizations.EndSectorServedUnitAsync(
            sectorGuid, relationshipGuid, request.EndDate, context, cancellationToken);
        return result.HasValue ? NoContent() : NotFound();
    }

    private bool TryGetActorGuid(out Guid actorGuid) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out actorGuid);

    private bool TryCreateContext(out SectorOperationContext context)
    {
        context = null!;
        if (!TryGetActorGuid(out var actorGuid))
        {
            return false;
        }

        var correlationId = HttpContext.Items.TryGetValue("CorrelationId", out var value) && value is Guid guid
            ? guid
            : Guid.NewGuid();
        context = new SectorOperationContext(
            actorGuid,
            correlationId,
            Activity.Current?.TraceId.ToString() ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return true;
    }

    private static ValidationProblemDetails ToProblem(ValidationResult validation) =>
        new(validation.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).ToArray()));
}
