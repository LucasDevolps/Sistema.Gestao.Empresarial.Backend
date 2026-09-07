namespace Sistema.Gestao.Empresarial.Application.Organizations;

public sealed record OrganizationCatalogListQuery(
    string? Search,
    bool? Active,
    int Page = 1,
    int PageSize = 50);

public sealed record SectorListQuery(
    string? Search,
    bool? Active,
    Guid? UnitGuid,
    int Page = 1,
    int PageSize = 50);

public sealed record OrganizationResponse(
    Guid Guid,
    string Name,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record HospitalUnitResponse(
    Guid Guid,
    string Name,
    bool Active,
    OrganizationReferenceResponse Organization,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SectorResponse(
    Guid Guid,
    string Name,
    bool Active,
    HospitalUnitReferenceResponse Unit,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OrganizationReferenceResponse(Guid Guid, string Name);

public sealed record HospitalUnitReferenceResponse(Guid Guid, string Name);

public sealed record HospitalUnitPageResponse(
    IReadOnlyCollection<HospitalUnitResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record SectorPageResponse(
    IReadOnlyCollection<SectorResponse> Items,
    int Page,
    int PageSize,
    int Total);

public interface IOrganizationCatalogService
{
    Task<OrganizationResponse> GetCurrentOrganizationAsync(
        Guid actorUserGuid,
        CancellationToken cancellationToken);

    Task<HospitalUnitPageResponse> ListHospitalUnitsAsync(
        Guid actorUserGuid,
        OrganizationCatalogListQuery query,
        CancellationToken cancellationToken);

    Task<HospitalUnitResponse?> GetHospitalUnitAsync(
        Guid actorUserGuid,
        Guid unitGuid,
        CancellationToken cancellationToken);

    Task<SectorPageResponse> ListSectorsAsync(
        Guid actorUserGuid,
        SectorListQuery query,
        CancellationToken cancellationToken);

    Task<SectorResponse?> GetSectorAsync(
        Guid actorUserGuid,
        Guid sectorGuid,
        CancellationToken cancellationToken);
}
