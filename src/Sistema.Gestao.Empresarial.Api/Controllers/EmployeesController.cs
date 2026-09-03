using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.Employees;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/funcionarios")]
public sealed class EmployeesController(
    IEmployeeService employees,
    IValidator<EmployeeListQuery> listValidator,
    IValidator<CreateEmployeeRequest> createValidator,
    IValidator<UpdateEmployeeRequest> updateValidator,
    IValidator<AddEmployeeActingUnitRequest> actingUnitValidator,
    IValidator<AddEmployeeSectorRequest> sectorValidator,
    IValidator<EndEmployeeRelationshipRequest> endRelationshipValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewEmployees)]
    [ProducesResponseType<EmployeePageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] Guid? actingUnitGuid,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new EmployeeListQuery(search, active, actingUnitGuid, page, pageSize);
        var validation = await listValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        return Ok(await employees.ListAsync(query, context, cancellationToken));
    }

    [HttpGet("{employeeGuid:guid}", Name = nameof(GetByGuid))]
    [RequirePermission(PermissionCodes.ViewEmployees)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByGuid(Guid employeeGuid, CancellationToken cancellationToken)
    {
        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await employees.GetAsync(employeeGuid, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CreateEmployees)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
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

        var response = await employees.CreateAsync(request, context, cancellationToken);
        return CreatedAtRoute(nameof(GetByGuid), new { employeeGuid = response.Guid }, response);
    }

    [HttpPut("{employeeGuid:guid}")]
    [RequirePermission(PermissionCodes.EditEmployees)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid employeeGuid,
        UpdateEmployeeRequest request,
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

        var response = await employees.UpdateAsync(employeeGuid, request, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPatch("{employeeGuid:guid}/status")]
    [RequirePermission(PermissionCodes.EditEmployees)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(
        Guid employeeGuid,
        ChangeEmployeeStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await employees.ChangeStatusAsync(
            employeeGuid, request.Active, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{employeeGuid:guid}/unidades-atuacao")]
    [RequirePermission(PermissionCodes.EditEmployees)]
    [ProducesResponseType<EmployeeActingUnitResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddActingUnit(
        Guid employeeGuid,
        AddEmployeeActingUnitRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await actingUnitValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await employees.AddActingUnitAsync(employeeGuid, request, context, cancellationToken);
        return response is null
            ? NotFound()
            : Created($"/api/funcionarios/{employeeGuid}/unidades-atuacao/{response.Guid}", response);
    }

    [HttpPost("{employeeGuid:guid}/unidades-atuacao/{relationshipGuid:guid}/encerrar")]
    [RequirePermission(PermissionCodes.EditEmployees)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EndActingUnit(
        Guid employeeGuid,
        Guid relationshipGuid,
        EndEmployeeRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await endRelationshipValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var result = await employees.EndActingUnitAsync(
            employeeGuid, relationshipGuid, request.EndDate, context, cancellationToken);
        return result.HasValue ? NoContent() : NotFound();
    }

    [HttpPost("{employeeGuid:guid}/setores")]
    [RequirePermission(PermissionCodes.EditEmployees)]
    [ProducesResponseType<EmployeeSectorResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddSector(
        Guid employeeGuid,
        AddEmployeeSectorRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await sectorValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var response = await employees.AddSectorAsync(employeeGuid, request, context, cancellationToken);
        return response is null
            ? NotFound()
            : Created($"/api/funcionarios/{employeeGuid}/setores/{response.Guid}", response);
    }

    [HttpPost("{employeeGuid:guid}/setores/{relationshipGuid:guid}/encerrar")]
    [RequirePermission(PermissionCodes.EditEmployees)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EndSector(
        Guid employeeGuid,
        Guid relationshipGuid,
        EndEmployeeRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await endRelationshipValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToProblem(validation));
        }

        if (!TryCreateContext(out var context))
        {
            return Unauthorized();
        }

        var result = await employees.EndSectorAsync(
            employeeGuid, relationshipGuid, request.EndDate, context, cancellationToken);
        return result.HasValue ? NoContent() : NotFound();
    }

    private bool TryCreateContext(out EmployeeOperationContext context)
    {
        context = null!;
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorGuid))
        {
            return false;
        }

        var correlationId = HttpContext.Items.TryGetValue("CorrelationId", out var value) && value is Guid guid
            ? guid
            : Guid.NewGuid();
        context = new EmployeeOperationContext(
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
