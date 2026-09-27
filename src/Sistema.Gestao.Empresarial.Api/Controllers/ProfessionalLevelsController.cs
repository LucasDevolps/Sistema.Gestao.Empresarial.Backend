using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

/// <summary>
/// Catálogo configurável de níveis profissionais. A exclusão é lógica e, como no
/// restante da API (que não expõe HTTP <c>DELETE</c>), é uma ação explícita:
/// <c>POST /api/niveis-profissionais/{guid}/excluir</c>, protegida por
/// <c>NIVEL_PROFISSIONAL_EDITAR</c> — o mesmo nível de permissão que a mudança de
/// status exige em profissões e cargos.
/// </summary>
[ApiController]
[Route("api/niveis-profissionais")]
public sealed class ProfessionalLevelsController(
    IProfessionalCatalogService catalogs,
    IValidator<ProfessionalCatalogListQuery> listValidator,
    IValidator<CreateProfessionalLevelRequest> createValidator,
    IValidator<UpdateProfessionalLevelRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewProfessionalLevels)]
    [ProducesResponseType<ProfessionalCatalogPageResponse<ProfessionalLevelResponse>>(StatusCodes.Status200OK)]
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
            ? Ok(await catalogs.ListLevelsAsync(query, cancellationToken))
            : ValidationProblem(ToProblem(validation));
    }

    [HttpGet("{levelGuid:guid}", Name = nameof(GetLevelByGuid))]
    [RequirePermission(PermissionCodes.ViewProfessionalLevels)]
    [ProducesResponseType<ProfessionalLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLevelByGuid(Guid levelGuid, CancellationToken cancellationToken)
    {
        var response = await catalogs.GetLevelAsync(levelGuid, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CreateProfessionalLevels)]
    [ProducesResponseType<ProfessionalLevelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        CreateProfessionalLevelRequest request,
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

        var response = await catalogs.CreateLevelAsync(request, context, cancellationToken);
        return CreatedAtRoute(nameof(GetLevelByGuid), new { levelGuid = response.Guid }, response);
    }

    [HttpPut("{levelGuid:guid}")]
    [RequirePermission(PermissionCodes.EditProfessionalLevels)]
    [ProducesResponseType<ProfessionalLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid levelGuid,
        UpdateProfessionalLevelRequest request,
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

        var response = await catalogs.UpdateLevelAsync(levelGuid, request, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{levelGuid:guid}/excluir")]
    [RequirePermission(PermissionCodes.EditProfessionalLevels)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid levelGuid, CancellationToken cancellationToken)
    {
        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        return await catalogs.DeleteLevelAsync(levelGuid, context, cancellationToken)
            ? NoContent()
            : NotFound();
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
