namespace Sistema.Gestao.Empresarial.Application.Authentication;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(Guid SessionId, string RefreshToken);

public sealed record AuthenticationResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    Guid SessionId,
    Guid UserGuid);

public sealed record AuthOperationContext(
    Guid CorrelationId,
    string TraceId,
    string? IpAddress,
    string? UserAgent);

public sealed record SessionTokenClaims(
    Guid UserGuid,
    Guid SessionId,
    string Jti,
    long SessionVersion);

public interface IAuthenticationService
{
    Task<AuthenticationResponse?> LoginAsync(
        LoginRequest request,
        AuthOperationContext context,
        CancellationToken cancellationToken);

    Task<AuthenticationResponse?> RefreshAsync(
        RefreshTokenRequest request,
        AuthOperationContext context,
        CancellationToken cancellationToken);

    Task LogoutAsync(
        SessionTokenClaims claims,
        AuthOperationContext context,
        CancellationToken cancellationToken);
}

public interface ISessionValidator
{
    Task<bool> ValidateAsync(SessionTokenClaims claims, CancellationToken cancellationToken);
}

public interface ICredentialHasher
{
    string HashPassword(string password);
    bool VerifyHashedPassword(string hash, string password);
}

public sealed record TokenMaterial(
    string AccessToken,
    string RefreshToken,
    string AccessTokenHash,
    string RefreshTokenHash,
    string Jti,
    DateTimeOffset AccessTokenExpiresAt);

public interface ITokenService
{
    TokenMaterial Create(Guid userGuid, Guid sessionId, long sessionVersion, DateTimeOffset now);
    string HashToken(string token);
}

public sealed record OperationalSession(
    Guid UserGuid,
    Guid SessionId,
    string Jti,
    long SessionVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset LastPersistedAt);

public enum OperationalSessionValidation
{
    Valid,
    CheckpointRequired,
    Missing,
    Invalid,
    Expired
}

public interface ISessionOperationalStore
{
    Task ReplaceActiveSessionAsync(
        OperationalSession session,
        IReadOnlyCollection<Guid> previousSessionIds,
        CancellationToken cancellationToken);

    Task<OperationalSessionValidation> ValidateAndTouchAsync(
        SessionTokenClaims claims,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task UpsertAsync(OperationalSession session, CancellationToken cancellationToken);

    Task RotateJtiAsync(Guid sessionId, string jti, DateTimeOffset now, CancellationToken cancellationToken);

    Task RemoveAsync(Guid userGuid, IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken);
}

public sealed class SessionStoreUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
