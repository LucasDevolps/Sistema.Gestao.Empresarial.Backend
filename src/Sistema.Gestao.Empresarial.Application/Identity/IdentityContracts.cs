namespace Sistema.Gestao.Empresarial.Application.Identity;

public sealed record IdentityListQuery(
    string? Search,
    bool? Active,
    int Page = 1,
    int PageSize = 50);

public sealed record CurrentUserResponse(
    Guid UserGuid,
    string Email,
    bool Active,
    long PermissionVersion,
    CurrentEmployeeResponse? Employee,
    IdentityOrganizationResponse? Organization,
    IdentityHospitalUnitResponse? HiringUnit,
    IReadOnlyCollection<string> Permissions);

public sealed record CurrentEmployeeResponse(
    Guid Guid,
    string RegistrationNumber,
    string Name,
    bool Active);

public sealed record IdentityOrganizationResponse(Guid Guid, string Name);

public sealed record IdentityHospitalUnitResponse(Guid Guid, string Name);

public sealed record UserPageResponse(
    IReadOnlyCollection<UserSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record UserSummaryResponse(
    Guid UserGuid,
    string Email,
    bool Active,
    bool TemporarilyBlocked,
    DateTimeOffset? LastLoginAt,
    long PermissionVersion,
    CurrentEmployeeResponse Employee,
    IdentityHospitalUnitResponse HiringUnit);

public interface IIdentityQueryService
{
    Task<CurrentUserResponse?> GetCurrentAsync(Guid userGuid, CancellationToken cancellationToken);

    Task<UserPageResponse> ListUsersAsync(
        Guid actorUserGuid,
        IdentityListQuery query,
        CancellationToken cancellationToken);
}
