namespace Sistema.Gestao.Empresarial.Application.Bootstrap;

public sealed record InitialAdminBootstrapRequest(
    string OrganizationName,
    string HospitalUnitName,
    string ProfessionName,
    string PositionName,
    string ProfessionalLevelCode,
    string AdministratorName,
    string AdministratorEmail,
    string? AdministratorPhone,
    DateOnly AdmissionDate,
    string Password);

public sealed record InitialAdminBootstrapResult(
    Guid OrganizationGuid,
    Guid HospitalUnitGuid,
    Guid EmployeeGuid,
    Guid UserGuid,
    Guid ProfileGuid,
    int GrantedPermissionCount);

public interface IInitialAdminBootstrapService
{
    Task<InitialAdminBootstrapResult> ExecuteAsync(
        InitialAdminBootstrapRequest request,
        CancellationToken cancellationToken);
}

public sealed class InitialAdminAlreadyProvisionedException()
    : InvalidOperationException("O bootstrap inicial já foi executado ou já existem usuários cadastrados.");
