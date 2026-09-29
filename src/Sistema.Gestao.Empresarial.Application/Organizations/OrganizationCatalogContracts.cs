using Sistema.Gestao.Empresarial.Domain.Organizacoes;

namespace Sistema.Gestao.Empresarial.Application.Organizations;

public sealed record OrganizationCatalogListQuery(
    string? Search,
    bool? Active,
    int Page = 1,
    int PageSize = 50,
    string? LegalName = null,
    string? Cnpj = null,
    string? Cnes = null,
    string? City = null,
    string? State = null,
    Guid? OrganizationGuid = null);

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

/// <summary>Consulta completa da unidade hospitalar.</summary>
public sealed record HospitalUnitResponse(
    Guid Guid, string Name, bool Active, OrganizationReferenceResponse Organization,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public string? LegalName { get; init; }
    public string? Cnpj { get; init; }
    public bool HasOwnCnpj { get; init; }
    public string? Cnes { get; init; }
    public TipoUnidadeHospitalar? UnitType { get; init; }
    public NaturezaUnidadeHospitalar? Nature { get; init; }
    public string? InternalCode { get; init; }
    public string? Acronym { get; init; }
    public DateOnly? ActivityStartDate { get; init; }
    public string? RegistrationStatus { get; init; }
    public DateOnly? OpeningDate { get; init; }
    public string? LegalNature { get; init; }
    public string? PrimaryCnae { get; init; }
    public string? SecondaryCnaes { get; init; }
    public string? StateRegistration { get; init; }
    public string? MunicipalRegistration { get; init; }
    public string? PostalCode { get; init; }
    public string? Street { get; init; }
    public string? Number { get; init; }
    public string? Complement { get; init; }
    public string? District { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? IbgeCode { get; init; }
    public string? Region { get; init; }
    public string? AreaCode { get; init; }
    public string? AddressReference { get; init; }
    public string? Phone { get; init; }
    public string? SecondaryPhone { get; init; }
    public string? Whatsapp { get; init; }
    public string? Email { get; init; }
    public string? AdministrativeEmail { get; init; }
    public string? Website { get; init; }
    public string? Extension { get; init; }
    public string? AdministrativeResponsibleName { get; init; }
    public string? AdministrativeResponsibleRole { get; init; }
    public string? AdministrativeResponsibleEmail { get; init; }
    public string? AdministrativeResponsiblePhone { get; init; }
    public string? TechnicalResponsibleName { get; init; }
    public string? TechnicalResponsibleProfession { get; init; }
    public string? TechnicalResponsibleCouncil { get; init; }
    public string? TechnicalResponsibleCouncilNumber { get; init; }
    public string? TechnicalResponsibleCouncilState { get; init; }
    public string? TechnicalResponsibleEmail { get; init; }
    public string? TechnicalResponsiblePhone { get; init; }
    public string? ClinicalDirectorName { get; init; }
    public string? ClinicalDirectorCrm { get; init; }
    public string? ClinicalDirectorCrmState { get; init; }
    public string? ClinicalDirectorEmail { get; init; }
    public string? ClinicalDirectorPhone { get; init; }
    public string? SanitaryPermit { get; init; }
    public DateOnly? SanitaryPermitExpiry { get; init; }
    public string? OperatingLicense { get; init; }
    public DateOnly? OperatingLicenseExpiry { get; init; }
    public string? RegulatoryNotes { get; init; }
    public bool? Open24Hours { get; init; }
    public bool? HasEmergencyRoom { get; init; }
    public bool? HasInpatientCare { get; init; }
    public bool? HasIcu { get; init; }
    public int? TotalBeds { get; init; }
    public int? IcuBeds { get; init; }
    public bool? HasSurgicalCenter { get; init; }
    public bool? HasMaternity { get; init; }
    public bool? HasOutpatientCare { get; init; }
    public string? Notes { get; init; }
}

public sealed record OrganizationReferenceResponse(Guid Guid, string Name);

public sealed record HospitalUnitReferenceResponse(Guid Guid, string Name);

public sealed record SectorCategoryReferenceResponse(Guid Guid, string Name);

public sealed record SectorResponsibleResponse(Guid Guid, string Name, string RegistrationNumber);

public sealed record HospitalUnitPageResponse(
    IReadOnlyCollection<HospitalUnitSummaryResponse> Items,
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
    Task<HospitalUnitResponse> CreateHospitalUnitAsync(HospitalUnitRegistrationRequest request, HospitalUnitOperationContext context, CancellationToken cancellationToken);
    Task<HospitalUnitResponse?> UpdateHospitalUnitAsync(Guid unitGuid, HospitalUnitRegistrationRequest request, HospitalUnitOperationContext context, CancellationToken cancellationToken);
    Task<HospitalUnitResponse?> ChangeHospitalUnitStatusAsync(Guid unitGuid, bool active, HospitalUnitOperationContext context, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<HospitalUnitSummaryResponse>> FindHospitalUnitDuplicatesAsync(Guid actorUserGuid, HospitalUnitDuplicateQuery query, CancellationToken cancellationToken);

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
