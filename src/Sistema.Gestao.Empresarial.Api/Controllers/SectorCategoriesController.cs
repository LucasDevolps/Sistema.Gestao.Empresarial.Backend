using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.Organizations;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/categorias-setor")]
public sealed class SectorCategoriesController(
    IOrganizationCatalogService organizations,
    IValidator<SectorCategoryListQuery> listValidator,
    IValidator<CreateSectorCategoryRequest> createValidator,
    IValidator<UpdateSectorCategoryRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewSectorCategories)]
    [ProducesResponseType<SectorCategoryPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new SectorCategoryListQuery(search, active, page, pageSize);
        var validation = await listValidator.ValidateAsync(query, cancellationToken);
        return validation.IsValid
            ? Ok(await organizations.ListSectorCategoriesAsync(query, cancellationToken))
            : ValidationProblem(ToProblem(validation));
    }

    [HttpGet("{categoryGuid:guid}", Name = nameof(GetSectorCategoryByGuid))]
    [RequirePermission(PermissionCodes.ViewSectorCategories)]
    [ProducesResponseType<SectorCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSectorCategoryByGuid(Guid categoryGuid, CancellationToken cancellationToken)
    {
        var response = await organizations.GetSectorCategoryAsync(categoryGuid, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CreateSectorCategories)]
    [ProducesResponseType<SectorCategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        CreateSectorCategoryRequest request,
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

        var response = await organizations.CreateSectorCategoryAsync(request, context, cancellationToken);
        return CreatedAtRoute(nameof(GetSectorCategoryByGuid), new { categoryGuid = response.Guid }, response);
    }

    [HttpPut("{categoryGuid:guid}")]
    [RequirePermission(PermissionCodes.EditSectorCategories)]
    [ProducesResponseType<SectorCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid categoryGuid,
        UpdateSectorCategoryRequest request,
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

        var response = await organizations.UpdateSectorCategoryAsync(
            categoryGuid, request, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPatch("{categoryGuid:guid}/status")]
    [RequirePermission(PermissionCodes.EditSectorCategories)]
    [ProducesResponseType<SectorCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(
        Guid categoryGuid,
        ChangeSectorCategoryStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await organizations.ChangeSectorCategoryStatusAsync(
            categoryGuid, request.Active, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private bool TryCreateContext(out SectorOperationContext context)
    {
        context = null!;
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorGuid))
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
