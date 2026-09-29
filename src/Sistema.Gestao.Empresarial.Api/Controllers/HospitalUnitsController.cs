using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.Organizations;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/unidades-hospitalares")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class HospitalUnitsController(
    IOrganizationCatalogService organizations,
    IValidator<OrganizationCatalogListQuery> listValidator,
    IValidator<HospitalUnitRegistrationRequest> registrationValidator,
    IValidator<HospitalUnitDuplicateQuery> duplicateValidator,
    ICnpjLookupService cnpjLookup,
    ICepLookupService cepLookup) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewHospitalUnits)]
    [ProducesResponseType<HospitalUnitPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] string? legalName,
        [FromQuery] string? cnpj,
        [FromQuery] string? cnes,
        [FromQuery] string? city,
        [FromQuery] string? state,
        [FromQuery] Guid? organizationGuid,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new OrganizationCatalogListQuery(search, active, page, pageSize, legalName, cnpj, cnes, city, state, organizationGuid);
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
    [RequirePermission(PermissionCodes.ViewHospitalUnits)]
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

    [HttpPost]
    [RequirePermission(PermissionCodes.CreateHospitalUnits)]
    [ProducesResponseType<HospitalUnitResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(HospitalUnitRegistrationRequest request, CancellationToken cancellationToken)
    {
        var validation = await registrationValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToProblem(validation));
        if (!TryCreateContext(out var context)) return Unauthorized();
        var response = await organizations.CreateHospitalUnitAsync(request, context, cancellationToken);
        return CreatedAtAction(nameof(GetByGuid), new { unitGuid = response.Guid }, response);
    }

    [HttpPut("{unitGuid:guid}")]
    [RequirePermission(PermissionCodes.EditHospitalUnits)]
    [ProducesResponseType<HospitalUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid unitGuid, HospitalUnitRegistrationRequest request, CancellationToken cancellationToken)
    {
        var validation = await registrationValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToProblem(validation));
        if (!TryCreateContext(out var context)) return Unauthorized();
        var response = await organizations.UpdateHospitalUnitAsync(unitGuid, request, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPatch("{unitGuid:guid}/status")]
    [RequirePermission(PermissionCodes.EditHospitalUnits)]
    [ProducesResponseType<HospitalUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(Guid unitGuid, ChangeHospitalUnitStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryCreateContext(out var context)) return Unauthorized();
        var response = await organizations.ChangeHospitalUnitStatusAsync(unitGuid, request.Active, context, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("possiveis-duplicidades")]
    [RequirePermission(PermissionCodes.ViewHospitalUnits)]
    [ProducesResponseType<IReadOnlyCollection<HospitalUnitSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FindDuplicates(HospitalUnitDuplicateQuery request, CancellationToken cancellationToken)
    {
        var validation = await duplicateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToProblem(validation));
        if (!TryGetActorGuid(out var actor)) return Unauthorized();
        return Ok(await organizations.FindHospitalUnitDuplicatesAsync(actor, request, cancellationToken));
    }

    [HttpGet("consulta-cnpj")]
    [RequirePermission(PermissionCodes.ViewHospitalUnits)]
    [ProducesResponseType<CnpjLookupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> LookupCnpj([FromQuery] string cnpj, CancellationToken cancellationToken)
    {
        if (!TryGetActorGuid(out var actor)) return Unauthorized();
        await organizations.GetCurrentOrganizationAsync(actor, cancellationToken);
        return LookupResponse(await cnpjLookup.LookupAsync(cnpj, cancellationToken), "cnpj", "CNPJ");
    }

    [HttpGet("consulta-cep")]
    [RequirePermission(PermissionCodes.ViewHospitalUnits)]
    [ProducesResponseType<CepLookupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> LookupCep([FromQuery] string cep, CancellationToken cancellationToken)
    {
        if (!TryGetActorGuid(out var actor)) return Unauthorized();
        await organizations.GetCurrentOrganizationAsync(actor, cancellationToken);
        return LookupResponse(await cepLookup.LookupAsync(cep, cancellationToken), "cep", "CEP");
    }

    private IActionResult LookupResponse<T>(LookupResult<T> result, string field, string label) => result.Outcome switch
    {
        LookupOutcome.Found => Ok(result.Data),
        LookupOutcome.NotFound => Problem(statusCode: 404, title: $"{label} não encontrado"),
        LookupOutcome.InvalidInput => ValidationProblem(new ValidationProblemDetails(
            new Dictionary<string, string[]> { [field] = [$"{label} inválido."] })),
        _ => Problem(statusCode: 503, title: "Consulta temporariamente indisponível.",
            detail: "Informe os dados manualmente para continuar o cadastro.")
    };

    private bool TryCreateContext(out HospitalUnitOperationContext context)
    {
        context = null!;
        if (!TryGetActorGuid(out var actor)) return false;
        var correlation = HttpContext.Items.TryGetValue("CorrelationId", out var value) && value is Guid guid ? guid : Guid.NewGuid();
        context = new(actor, correlation, Activity.Current?.TraceId.ToString() ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return true;
    }

    private bool TryGetActorGuid(out Guid actorGuid) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out actorGuid);

    private static ValidationProblemDetails ToProblem(ValidationResult validation) =>
        new(validation.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).ToArray()));
}
