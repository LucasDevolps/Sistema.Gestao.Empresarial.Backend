namespace Sistema.Gestao.Empresarial.Application.WorkSchedules;

public sealed record WorkScheduleListQuery(
    string? Search,
    bool? Active,
    int Page = 1,
    int PageSize = 50);

public sealed record CreateWorkScheduleRequest(
    string Name,
    int ConsecutiveWorkDays,
    int RestDays,
    int MaximumConsecutiveWorkDays,
    string? Description);

public sealed record UpdateWorkScheduleRequest(
    string Name,
    int ConsecutiveWorkDays,
    int RestDays,
    int MaximumConsecutiveWorkDays,
    string? Description);

public sealed record ChangeWorkScheduleStatusRequest(bool Active);

public sealed record WorkScheduleResponse(
    Guid Guid,
    string Name,
    int ConsecutiveWorkDays,
    int RestDays,
    int MaximumConsecutiveWorkDays,
    string? Description,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkSchedulePageResponse(
    IReadOnlyCollection<WorkScheduleResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record WorkScheduleOperationContext(
    Guid ActorUserGuid,
    Guid CorrelationId,
    string TraceId,
    string? IpAddress);

public interface IWorkScheduleService
{
    Task<WorkSchedulePageResponse> ListAsync(WorkScheduleListQuery query, CancellationToken cancellationToken);

    Task<WorkScheduleResponse?> GetAsync(Guid workScheduleGuid, CancellationToken cancellationToken);

    Task<WorkScheduleResponse> CreateAsync(
        CreateWorkScheduleRequest request,
        WorkScheduleOperationContext context,
        CancellationToken cancellationToken);

    Task<WorkScheduleResponse?> UpdateAsync(
        Guid workScheduleGuid,
        UpdateWorkScheduleRequest request,
        WorkScheduleOperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Inativa ou reativa a jornada. Retorna <c>null</c> quando ela não existe.
    /// A operação é idempotente: repetir o mesmo status não gera auditoria.
    /// </summary>
    Task<WorkScheduleResponse?> ChangeStatusAsync(
        Guid workScheduleGuid,
        bool active,
        WorkScheduleOperationContext context,
        CancellationToken cancellationToken);
}
