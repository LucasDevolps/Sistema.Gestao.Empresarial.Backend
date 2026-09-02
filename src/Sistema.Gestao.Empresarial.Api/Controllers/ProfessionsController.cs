using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/profissoes")]
public sealed class ProfessionsController(
    IProfessionalCatalogService catalogs,
    IValidator<ProfessionalCatalogListQuery> listValidator,
    IValidator<CreateProfessionalCatalogRequest> createValidator,
    IValidator<UpdateProfessionalCatalogRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewProfessions)]
    [ProducesResponseType<ProfessionalCatalogPageResponse<ProfessionResponse>>(StatusCodes.Status200OK)]
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
            ? Ok(await catalogs.ListProfessionsAsync(query, cancellationToken))
            : ValidationProblem(ToProblem(validation));
    }

    [HttpGet("{professionGuid:guid}", Name = nameof(GetProfessionByGuid))]
    [RequirePermission(PermissionCodes.ViewProfessions)]
    [ProducesResponseType<ProfessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfessionByGuid(Guid professionGuid, CancellationToken cancellationToken)
    {
        var response = await catalogs.GetProfessionAsync(professionGuid, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CreateProfessions)]
    [ProducesResponseType<ProfessionResponse>(StatusCodes.Status201Created)]
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

        var response = await catalogs.CreateProfessionAsync(request, context, cancellationToken);
        return CreatedAtRoute(nameof(GetProfessionByGuid), new { professionGuid = response.Guid }, response);
    }

    [HttpPut("{professionGuid:guid}")]
    [RequirePermission(PermissionCodes.EditProfessions)]
    [ProducesResponseType<ProfessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid professionGuid,
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

        var response = await catalogs.UpdateProfessionAsync(
            professionGuid, request, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPatch("{professionGuid:guid}/status")]
    [RequirePermission(PermissionCodes.EditProfessions)]
    [ProducesResponseType<ProfessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(
        Guid professionGuid,
        ChangeProfessionalCatalogStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await catalogs.ChangeProfessionStatusAsync(
            professionGuid, request.Active, context, cancellationToken);
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
