namespace Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;

public sealed record ProfessionalCatalogListQuery(
    string? Search,
    bool? Active,
    int Page = 1,
    int PageSize = 50);

public sealed record CreateProfessionalCatalogRequest(string Name, string? Description);
public sealed record UpdateProfessionalCatalogRequest(string Name, string? Description);
public sealed record ChangeProfessionalCatalogStatusRequest(bool Active);

public sealed record CreateProfessionalLevelRequest(string Code, string Name, int Order);
public sealed record UpdateProfessionalLevelRequest(string Code, string Name, int Order);

public sealed record ProfessionalCatalogPageResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record ProfessionResponse(
    Guid Guid,
    string Name,
    string? Description,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PositionResponse(
    Guid Guid,
    string Name,
    string? Description,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProfessionalLevelResponse(
    Guid Guid,
    string Code,
    string Name,
    int Order,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProfessionalCatalogOperationContext(
    Guid ActorUserGuid,
    Guid CorrelationId,
    string TraceId,
    string? IpAddress);

public interface IProfessionalCatalogService
{
    Task<ProfessionalCatalogPageResponse<ProfessionResponse>> ListProfessionsAsync(
        ProfessionalCatalogListQuery query,
        CancellationToken cancellationToken);

    Task<ProfessionResponse?> GetProfessionAsync(Guid professionGuid, CancellationToken cancellationToken);

    Task<ProfessionResponse> CreateProfessionAsync(
        CreateProfessionalCatalogRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken);

    Task<ProfessionResponse?> UpdateProfessionAsync(
        Guid professionGuid,
        UpdateProfessionalCatalogRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken);

    Task<ProfessionResponse?> ChangeProfessionStatusAsync(
        Guid professionGuid,
        bool active,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken);

    Task<ProfessionalCatalogPageResponse<PositionResponse>> ListPositionsAsync(
        ProfessionalCatalogListQuery query,
        CancellationToken cancellationToken);

    Task<PositionResponse?> GetPositionAsync(Guid positionGuid, CancellationToken cancellationToken);

    Task<PositionResponse> CreatePositionAsync(
        CreateProfessionalCatalogRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken);

    Task<PositionResponse?> UpdatePositionAsync(
        Guid positionGuid,
        UpdateProfessionalCatalogRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken);

    Task<PositionResponse?> ChangePositionStatusAsync(
        Guid positionGuid,
        bool active,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken);

    Task<ProfessionalCatalogPageResponse<ProfessionalLevelResponse>> ListLevelsAsync(
        ProfessionalCatalogListQuery query,
        CancellationToken cancellationToken);

    Task<ProfessionalLevelResponse?> GetLevelAsync(Guid levelGuid, CancellationToken cancellationToken);

    Task<ProfessionalLevelResponse> CreateLevelAsync(
        CreateProfessionalLevelRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken);

    Task<ProfessionalLevelResponse?> UpdateLevelAsync(
        Guid levelGuid,
        UpdateProfessionalLevelRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exclui logicamente o nível. Retorna <c>false</c> quando o nível não existe
    /// (ou já foi excluído); lança <c>DomainException</c> quando ele está vinculado
    /// a algum funcionário.
    /// </summary>
    Task<bool> DeleteLevelAsync(
        Guid levelGuid,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken);
}
