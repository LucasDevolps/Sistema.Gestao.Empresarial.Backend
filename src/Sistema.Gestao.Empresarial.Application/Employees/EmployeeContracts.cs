namespace Sistema.Gestao.Empresarial.Application.Employees;

public sealed record CreateEmployeeRequest(
    string Name,
    string Email,
    string? Phone,
    Guid ProfessionGuid,
    Guid PositionGuid,
    Guid LevelGuid,
    Guid HiringUnitGuid,
    DateOnly AdmissionDate,
    IReadOnlyCollection<CreateEmployeeActingUnitRequest>? ActingUnits,
    IReadOnlyCollection<CreateEmployeeSectorRequest>? Sectors);

public sealed record CreateEmployeeActingUnitRequest(Guid UnitGuid, DateOnly StartDate);
public sealed record CreateEmployeeSectorRequest(Guid SectorGuid, DateOnly StartDate);

public sealed record UpdateEmployeeRequest(
    string Name,
    string Email,
    string? Phone,
    Guid ProfessionGuid,
    Guid PositionGuid,
    Guid LevelGuid);

public sealed record ChangeEmployeeStatusRequest(bool Active);
public sealed record AddEmployeeActingUnitRequest(Guid UnitGuid, DateOnly StartDate);
public sealed record AddEmployeeSectorRequest(Guid SectorGuid, DateOnly StartDate);
public sealed record EndEmployeeRelationshipRequest(DateOnly EndDate);

public sealed record EmployeeListQuery(
    string? Search,
    bool? Active,
    Guid? ActingUnitGuid,
    int Page = 1,
    int PageSize = 50);

public sealed record EmployeePageResponse(
    IReadOnlyCollection<EmployeeSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record EmployeeSummaryResponse(
    Guid Guid,
    string RegistrationNumber,
    string Name,
    string Email,
    bool Active,
    EmployeeReferenceResponse Profession,
    EmployeeReferenceResponse Position,
    EmployeeLevelResponse Level,
    EmployeeReferenceResponse HiringUnit);

public sealed record EmployeeResponse(
    Guid Guid,
    string RegistrationNumber,
    string Name,
    string Email,
    string? Phone,
    DateOnly AdmissionDate,
    bool Active,
    EmployeeReferenceResponse Profession,
    EmployeeReferenceResponse Position,
    EmployeeLevelResponse Level,
    EmployeeReferenceResponse HiringUnit,
    IReadOnlyCollection<EmployeeActingUnitResponse> ActingUnits,
    IReadOnlyCollection<EmployeeSectorResponse> Sectors,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EmployeeReferenceResponse(Guid Guid, string Name);
public sealed record EmployeeLevelResponse(Guid Guid, string Code, string Name);

public sealed record EmployeeActingUnitResponse(
    Guid Guid,
    Guid UnitGuid,
    string UnitName,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool Active);

public sealed record EmployeeSectorResponse(
    Guid Guid,
    Guid SectorGuid,
    string SectorName,
    Guid UnitGuid,
    string UnitName,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool Active);

public sealed record EmployeeOperationContext(
    Guid ActorUserGuid,
    Guid CorrelationId,
    string TraceId,
    string? IpAddress);

public interface IEmployeeService
{
    Task<EmployeePageResponse> ListAsync(EmployeeListQuery query, EmployeeOperationContext context, CancellationToken cancellationToken);
    Task<EmployeeResponse?> GetAsync(Guid employeeGuid, EmployeeOperationContext context, CancellationToken cancellationToken);
    Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, EmployeeOperationContext context, CancellationToken cancellationToken);
    Task<EmployeeResponse?> UpdateAsync(Guid employeeGuid, UpdateEmployeeRequest request, EmployeeOperationContext context, CancellationToken cancellationToken);
    Task<EmployeeResponse?> ChangeStatusAsync(Guid employeeGuid, bool active, EmployeeOperationContext context, CancellationToken cancellationToken);
    Task<EmployeeActingUnitResponse?> AddActingUnitAsync(Guid employeeGuid, AddEmployeeActingUnitRequest request, EmployeeOperationContext context, CancellationToken cancellationToken);
    Task<bool?> EndActingUnitAsync(Guid employeeGuid, Guid relationshipGuid, DateOnly endDate, EmployeeOperationContext context, CancellationToken cancellationToken);
    Task<EmployeeSectorResponse?> AddSectorAsync(Guid employeeGuid, AddEmployeeSectorRequest request, EmployeeOperationContext context, CancellationToken cancellationToken);
    Task<bool?> EndSectorAsync(Guid employeeGuid, Guid relationshipGuid, DateOnly endDate, EmployeeOperationContext context, CancellationToken cancellationToken);
}
