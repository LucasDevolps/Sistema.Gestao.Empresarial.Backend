namespace Sistema.Gestao.Empresarial.Application.Authorization;

public static class PermissionCodes
{
    public const string ManageUserPermissions = "USUARIO_GERENCIAR_PERMISSOES";
}

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(Guid userGuid, string permission, CancellationToken cancellationToken);
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userGuid, CancellationToken cancellationToken);
}

public sealed record SetUserPermissionRequest(bool Granted);

public sealed record UserPermissionsResponse(Guid UserGuid, long Version, IReadOnlyCollection<string> Permissions);

public enum PermissionChangeResult
{
    Changed,
    Unchanged,
    Forbidden,
    UserNotFound,
    PermissionNotFound
}

public interface IPermissionAdministrationService
{
    Task<UserPermissionsResponse?> GetAsync(Guid userGuid, CancellationToken cancellationToken);

    Task<PermissionChangeResult> SetDirectPermissionAsync(
        Guid actorUserGuid,
        Guid targetUserGuid,
        string permissionCode,
        bool granted,
        Guid correlationId,
        string traceId,
        string? ipAddress,
        CancellationToken cancellationToken);
}
