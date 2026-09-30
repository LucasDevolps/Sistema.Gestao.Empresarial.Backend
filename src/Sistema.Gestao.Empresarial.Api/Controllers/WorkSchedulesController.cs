using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.WorkSchedules;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

/// <summary>
/// Tipos de jornada de trabalho (6x1, 5x2 etc.) usados futuramente nas escalas. Não
/// há exclusão: a jornada é inativada/reativada por <c>PATCH .../status</c>, preservando
/// o histórico.
/// </summary>
[ApiController]
[Route("api/jornadas-trabalho")]
public sealed class WorkSchedulesController(
    IWorkScheduleService schedules,
    IValidator<WorkScheduleListQuery> listValidator,
    IValidator<CreateWorkScheduleRequest> createValidator,
    IValidator<UpdateWorkScheduleRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewWorkSchedules)]
    [ProducesResponseType<WorkSchedulePageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new WorkScheduleListQuery(search, active, page, pageSize);
        var validation = await listValidator.ValidateAsync(query, cancellationToken);
        return validation.IsValid
            ? Ok(await schedules.ListAsync(query, cancellationToken))
            : ValidationProblem(ToProblem(validation));
    }

    [HttpGet("{workScheduleGuid:guid}", Name = nameof(GetWorkScheduleByGuid))]
    [RequirePermission(PermissionCodes.ViewWorkSchedules)]
    [ProducesResponseType<WorkScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkScheduleByGuid(Guid workScheduleGuid, CancellationToken cancellationToken)
    {
        var response = await schedules.GetAsync(workScheduleGuid, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CreateWorkSchedules)]
    [ProducesResponseType<WorkScheduleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        CreateWorkScheduleRequest request,
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

        var response = await schedules.CreateAsync(request, context, cancellationToken);
        return CreatedAtRoute(nameof(GetWorkScheduleByGuid), new { workScheduleGuid = response.Guid }, response);
    }

    [HttpPut("{workScheduleGuid:guid}")]
    [RequirePermission(PermissionCodes.EditWorkSchedules)]
    [ProducesResponseType<WorkScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid workScheduleGuid,
        UpdateWorkScheduleRequest request,
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

        var response = await schedules.UpdateAsync(workScheduleGuid, request, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPatch("{workScheduleGuid:guid}/status")]
    [RequirePermission(PermissionCodes.EditWorkSchedules)]
    [ProducesResponseType<WorkScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(
        Guid workScheduleGuid,
        ChangeWorkScheduleStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await schedules.ChangeStatusAsync(workScheduleGuid, request.Active, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private bool TryCreateContext(out WorkScheduleOperationContext context)
    {
        context = null!;
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorGuid))
        {
            return false;
        }

        var correlationId = HttpContext.Items.TryGetValue("CorrelationId", out var value) && value is Guid guid
            ? guid
            : Guid.NewGuid();
        context = new WorkScheduleOperationContext(
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
