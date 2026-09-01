using System.Diagnostics;
using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Gestao.Empresarial.Api.Security;
using Sistema.Gestao.Empresarial.Application.Authentication;

namespace Sistema.Gestao.Empresarial.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshTokenRequest> refreshValidator) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validation.Errors.GroupBy(x => x.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).ToArray())));
        }

        var result = await authenticationService.LoginAsync(request, CreateContext(), cancellationToken);
        return result is null
            ? Unauthorized(new ProblemDetails { Title = "Credenciais inválidas.", Status = StatusCodes.Status401Unauthorized })
            : Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var validation = await refreshValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validation.Errors.GroupBy(x => x.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).ToArray())));
        }

        var result = await authenticationService.RefreshAsync(request, CreateContext(), cancellationToken);
        return result is null
            ? Unauthorized(new ProblemDetails { Title = "Refresh token inválido ou sessão expirada.", Status = StatusCodes.Status401Unauthorized })
            : Ok(result);
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicies.ActiveSession)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!TryGetSessionClaims(out var claims))
        {
            return Unauthorized();
        }

        await authenticationService.LogoutAsync(claims, CreateContext(), cancellationToken);
        return NoContent();
    }

    private AuthOperationContext CreateContext()
    {
        var correlationId = HttpContext.Items.TryGetValue("CorrelationId", out var value) && value is Guid guid
            ? guid
            : Guid.NewGuid();
        return new AuthOperationContext(
            correlationId,
            Activity.Current?.TraceId.ToString() ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }

    private bool TryGetSessionClaims(out SessionTokenClaims claims)
    {
        claims = null!;
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userGuid)
            || !Guid.TryParse(User.FindFirst("sid")?.Value, out var sessionId)
            || string.IsNullOrWhiteSpace(User.FindFirst("jti")?.Value)
            || !long.TryParse(User.FindFirst("session_version")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            return false;
        }

        claims = new SessionTokenClaims(userGuid, sessionId, User.FindFirst("jti")!.Value, version);
        return true;
    }
}
