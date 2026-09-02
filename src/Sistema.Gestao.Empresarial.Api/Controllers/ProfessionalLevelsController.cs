using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/niveis-profissionais")]
public sealed class ProfessionalLevelsController(IProfessionalCatalogService catalogs) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ViewProfessionalLevels)]
    [ProducesResponseType<IReadOnlyCollection<ProfessionalLevelResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool? active,
        CancellationToken cancellationToken) =>
        Ok(await catalogs.ListLevelsAsync(active, cancellationToken));

    [HttpGet("{levelGuid:guid}")]
    [RequirePermission(PermissionCodes.ViewProfessionalLevels)]
    [ProducesResponseType<ProfessionalLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByGuid(Guid levelGuid, CancellationToken cancellationToken)
    {
        var response = await catalogs.GetLevelAsync(levelGuid, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
