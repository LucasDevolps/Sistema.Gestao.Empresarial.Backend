using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.Organizations;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/organizacoes")]
public sealed class OrganizationsController(IOrganizationCatalogService organizations) : ControllerBase
{
    [HttpGet("atual")]
    [RequirePermission(PermissionCodes.ViewEmployees)]
    [ProducesResponseType<OrganizationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        if (!TryGetActorGuid(out var actorGuid))
        {
            return Unauthorized();
        }

        return Ok(await organizations.GetCurrentOrganizationAsync(actorGuid, cancellationToken));
    }

    private bool TryGetActorGuid(out Guid actorGuid) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out actorGuid);
}
