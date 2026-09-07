using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Application.Identity;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Identity;

public sealed class IdentityQueryService(
    AppDbContext dbContext,
    IPermissionChecker permissionChecker,
    TimeProvider timeProvider) : IIdentityQueryService
{
    public async Task<CurrentUserResponse?> GetCurrentAsync(
        Guid userGuid,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Usuarios.AsNoTracking()
            .Where(x => x.Guid == userGuid && x.Ativo)
            .Select(x => new CurrentIdentityProjection(
                x.Guid,
                x.Email,
                x.Ativo,
                x.VersaoPermissoes,
                x.FuncionarioId.HasValue ? x.Funcionario!.Guid : null,
                x.FuncionarioId.HasValue ? x.Funcionario!.Matricula : null,
                x.FuncionarioId.HasValue ? x.Funcionario!.Nome : null,
                x.FuncionarioId.HasValue ? x.Funcionario!.Ativo : null,
                x.FuncionarioId.HasValue ? x.Funcionario!.UnidadeContratacao.Organizacao.Guid : null,
                x.FuncionarioId.HasValue ? x.Funcionario!.UnidadeContratacao.Organizacao.Nome : null,
                x.FuncionarioId.HasValue ? x.Funcionario!.UnidadeContratacao.Guid : null,
                x.FuncionarioId.HasValue ? x.Funcionario!.UnidadeContratacao.Nome : null))
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var permissions = await permissionChecker.GetPermissionsAsync(user.UserGuid, cancellationToken);
        var employee = user.EmployeeGuid.HasValue
            ? new CurrentEmployeeResponse(
                user.EmployeeGuid.Value,
                user.RegistrationNumber!,
                user.EmployeeName!,
                user.EmployeeActive!.Value)
            : null;
        var organization = user.OrganizationGuid.HasValue
            ? new IdentityOrganizationResponse(user.OrganizationGuid.Value, user.OrganizationName!)
            : null;
        var hiringUnit = user.HiringUnitGuid.HasValue
            ? new IdentityHospitalUnitResponse(user.HiringUnitGuid.Value, user.HiringUnitName!)
            : null;

        return new CurrentUserResponse(
            user.UserGuid,
            user.Email,
            user.Active,
            user.PermissionVersion,
            employee,
            organization,
            hiringUnit,
            [.. permissions.Order(StringComparer.Ordinal)]);
    }

    public async Task<UserPageResponse> ListUsersAsync(
        Guid actorUserGuid,
        IdentityListQuery query,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        var users = dbContext.Usuarios.AsNoTracking()
            .Where(x => x.FuncionarioId.HasValue
                && x.Funcionario!.UnidadeContratacao.OrganizacaoId == organizationId);
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            users = users.Where(x =>
                x.Email.Contains(search)
                || x.Funcionario!.Nome.Contains(search)
                || x.Funcionario.Matricula.Contains(search));
        }

        if (query.Active.HasValue)
        {
            users = users.Where(x => x.Ativo == query.Active.Value);
        }

        var now = timeProvider.GetUtcNow();
        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(x => x.Funcionario!.Nome)
            .ThenBy(x => x.Email)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new UserSummaryResponse(
                x.Guid,
                x.Email,
                x.Ativo,
                x.Bloqueado && x.BloqueadoAte > now,
                x.DataUltimoLogin,
                x.VersaoPermissoes,
                new CurrentEmployeeResponse(
                    x.Funcionario!.Guid,
                    x.Funcionario.Matricula,
                    x.Funcionario.Nome,
                    x.Funcionario.Ativo),
                new IdentityHospitalUnitResponse(
                    x.Funcionario.UnidadeContratacao.Guid,
                    x.Funcionario.UnidadeContratacao.Nome)))
            .ToListAsync(cancellationToken);

        return new UserPageResponse(items, query.Page, query.PageSize, total);
    }

    private async Task<long> GetActorOrganizationIdAsync(
        Guid actorUserGuid,
        CancellationToken cancellationToken) =>
        await dbContext.Usuarios.AsNoTracking()
            .Where(x => x.Guid == actorUserGuid
                && x.Ativo
                && x.FuncionarioId.HasValue
                && x.Funcionario!.Ativo)
            .Select(x => (long?)x.Funcionario!.UnidadeContratacao.OrganizacaoId)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new OrganizationAccessDeniedException();

    private sealed record CurrentIdentityProjection(
        Guid UserGuid,
        string Email,
        bool Active,
        long PermissionVersion,
        Guid? EmployeeGuid,
        string? RegistrationNumber,
        string? EmployeeName,
        bool? EmployeeActive,
        Guid? OrganizationGuid,
        string? OrganizationName,
        Guid? HiringUnitGuid,
        string? HiringUnitName);
}
