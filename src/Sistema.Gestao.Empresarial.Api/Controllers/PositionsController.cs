using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/cargos")]
public sealed class PositionsController(
    IProfessionalCatalogService catalogs,
    IValidator<ProfessionalCatalogListQuery> listValidator,
    IValidator<CreateProfessionalCatalogRequest> createValidator,
    IValidator<UpdateProfessionalCatalogRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewPositions)]
    [ProducesResponseType<ProfessionalCatalogPageResponse<PositionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ProfessionalCatalogListQuery(search, active, page, pageSize);
        var validation = await listValidator.ValidateAsync(query, cancellationToken);
        return validation.IsValid
            ? Ok(await catalogs.ListPositionsAsync(query, cancellationToken))
            : ValidationProblem(ToProblem(validation));
    }

    [HttpGet("{positionGuid:guid}", Name = nameof(GetPositionByGuid))]
    [RequirePermission(PermissionCodes.ViewPositions)]
    [ProducesResponseType<PositionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPositionByGuid(Guid positionGuid, CancellationToken cancellationToken)
    {
        var response = await catalogs.GetPositionAsync(positionGuid, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CreatePositions)]
    [ProducesResponseType<PositionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        CreateProfessionalCatalogRequest request,
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

        var response = await catalogs.CreatePositionAsync(request, context, cancellationToken);
        return CreatedAtRoute(nameof(GetPositionByGuid), new { positionGuid = response.Guid }, response);
    }

    [HttpPut("{positionGuid:guid}")]
    [RequirePermission(PermissionCodes.EditPositions)]
    [ProducesResponseType<PositionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid positionGuid,
        UpdateProfessionalCatalogRequest request,
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

        var response = await catalogs.UpdatePositionAsync(
            positionGuid, request, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPatch("{positionGuid:guid}/status")]
    [RequirePermission(PermissionCodes.EditPositions)]
    [ProducesResponseType<PositionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(
        Guid positionGuid,
        ChangeProfessionalCatalogStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await catalogs.ChangePositionStatusAsync(
            positionGuid, request.Active, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private bool TryCreateContext(out ProfessionalCatalogOperationContext context)
    {
        context = null!;
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorGuid))
        {
            return false;
        }

        var correlationId = HttpContext.Items.TryGetValue("CorrelationId", out var value) && value is Guid guid
            ? guid
            : Guid.NewGuid();
        context = new ProfessionalCatalogOperationContext(
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
