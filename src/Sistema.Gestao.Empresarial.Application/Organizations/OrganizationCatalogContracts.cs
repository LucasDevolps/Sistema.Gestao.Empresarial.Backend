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
    Guid? CategoryGuid,
    int Page = 1,
    int PageSize = 50);

public sealed record SectorCategoryListQuery(
    string? Search,
    bool? Active,
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

public sealed record OrganizationReferenceResponse(Guid Guid, string Name);

public sealed record HospitalUnitReferenceResponse(Guid Guid, string Name);

public sealed record SectorCategoryReferenceResponse(Guid Guid, string Name);

public sealed record SectorResponsibleResponse(Guid Guid, string Name, string RegistrationNumber);

public sealed record HospitalUnitPageResponse(
    IReadOnlyCollection<HospitalUnitResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record SectorSummaryResponse(
    Guid Guid,
    string Sigla,
    string Name,
    bool Active,
    bool CareRelated,
    bool AllowsScheduleAllocation,
    bool AllowsSharedActing,
    HospitalUnitReferenceResponse Unit,
    SectorCategoryReferenceResponse Category);

public sealed record SectorServedUnitResponse(
    Guid Guid,
    Guid UnitGuid,
    string UnitName,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool Active);

public sealed record SectorResponse(
    Guid Guid,
    string Sigla,
    string Name,
    bool Active,
    HospitalUnitReferenceResponse Unit,
    SectorCategoryReferenceResponse Category,
    string? Description,
    string? InternalLocation,
    string? Extension,
    string? Email,
    SectorResponsibleResponse? Responsible,
    bool CareRelated,
    bool AllowsScheduleAllocation,
    bool AllowsSharedActing,
    IReadOnlyCollection<SectorServedUnitResponse> ServedUnits,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SectorPageResponse(
    IReadOnlyCollection<SectorSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record SectorCategoryResponse(
    Guid Guid,
    string Name,
    string? Description,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SectorCategoryPageResponse(
    IReadOnlyCollection<SectorCategoryResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record CreateSectorServedUnitRequest(Guid UnitGuid, DateOnly StartDate);

public sealed record CreateSectorRequest(
    Guid UnitGuid,
    Guid CategoryGuid,
    string Name,
    string Sigla,
    string? Description,
    string? InternalLocation,
    string? Extension,
    string? Email,
    Guid? ResponsibleEmployeeGuid,
    bool CareRelated,
    bool AllowsScheduleAllocation,
    bool AllowsSharedActing,
    IReadOnlyCollection<CreateSectorServedUnitRequest>? ServedUnits);

public sealed record UpdateSectorRequest(
    Guid CategoryGuid,
    string Name,
    string Sigla,
    string? Description,
    string? InternalLocation,
    string? Extension,
    string? Email,
    Guid? ResponsibleEmployeeGuid,
    bool CareRelated,
    bool AllowsScheduleAllocation,
    bool AllowsSharedActing);

public sealed record ChangeSectorStatusRequest(bool Active);

public sealed record AddSectorServedUnitRequest(Guid UnitGuid, DateOnly StartDate);

public sealed record EndSectorServedUnitRequest(DateOnly EndDate);

public sealed record CreateSectorCategoryRequest(string Name, string? Description);

public sealed record UpdateSectorCategoryRequest(string Name, string? Description);

public sealed record ChangeSectorCategoryStatusRequest(bool Active);

public sealed record SectorOperationContext(
    Guid ActorUserGuid,
    Guid CorrelationId,
    string TraceId,
    string? IpAddress);

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

    Task<SectorResponse> CreateSectorAsync(
        CreateSectorRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken);

    Task<SectorResponse?> UpdateSectorAsync(
        Guid sectorGuid,
        UpdateSectorRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken);

    Task<SectorResponse?> ChangeSectorStatusAsync(
        Guid sectorGuid,
        bool active,
        SectorOperationContext context,
        CancellationToken cancellationToken);

    Task<SectorServedUnitResponse?> AddSectorServedUnitAsync(
        Guid sectorGuid,
        AddSectorServedUnitRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken);

    Task<bool?> EndSectorServedUnitAsync(
        Guid sectorGuid,
        Guid relationshipGuid,
        DateOnly endDate,
        SectorOperationContext context,
        CancellationToken cancellationToken);

    Task<SectorCategoryPageResponse> ListSectorCategoriesAsync(
        SectorCategoryListQuery query,
        CancellationToken cancellationToken);

    Task<SectorCategoryResponse?> GetSectorCategoryAsync(
        Guid categoryGuid,
        CancellationToken cancellationToken);

    Task<SectorCategoryResponse> CreateSectorCategoryAsync(
        CreateSectorCategoryRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken);

    Task<SectorCategoryResponse?> UpdateSectorCategoryAsync(
        Guid categoryGuid,
        UpdateSectorCategoryRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken);

    Task<SectorCategoryResponse?> ChangeSectorCategoryStatusAsync(
        Guid categoryGuid,
        bool active,
        SectorOperationContext context,
        CancellationToken cancellationToken);
}
