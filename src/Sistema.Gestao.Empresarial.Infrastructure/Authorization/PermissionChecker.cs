using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Authorization;

public sealed class PermissionChecker(
    AppDbContext dbContext,
    IPermissionCache cache,
    PermissionMetrics metrics,
    ILogger<PermissionChecker> logger) : IPermissionChecker
{
    public async Task<bool> HasPermissionAsync(
        Guid userGuid,
        string permission,
        CancellationToken cancellationToken)
    {
        if (userGuid == Guid.Empty || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var permissions = await GetPermissionsAsync(userGuid, cancellationToken);
        return permissions.Contains(permission.Trim().ToUpperInvariant());
    }

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(
        Guid userGuid,
        CancellationToken cancellationToken)
    {
        PermissionCacheEntry? cached = null;
        try
        {
            cached = await cache.GetAsync(userGuid, cancellationToken);
            if (cached?.Ready == true)
            {
                metrics.Hit();
                return new HashSet<string>(cached.Permissions, StringComparer.Ordinal);
            }
            metrics.Miss();
        }
        catch (PermissionCacheUnavailableException exception)
        {
            metrics.Fallback();
            logger.LogWarning(exception, "Fallback Redis para SQL ao carregar permissões");
        }

        var snapshot = await LoadFromSqlAsync(userGuid, cancellationToken);
        if (snapshot is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (cached is { Ready: false } && cached.Version > snapshot.Value.Version)
        {
            metrics.BarrierDenied();
            logger.LogWarning(
                "Permissões negadas por barreira de versão. Cache {CacheVersion}, SQL {SqlVersion}",
                cached.Version,
                snapshot.Value.Version);
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var entry = new PermissionCacheEntry(snapshot.Value.Version, true, [.. snapshot.Value.Permissions.Order()]);
        try
        {
            await cache.PublishAsync(userGuid, entry, cancellationToken);
        }
        catch (PermissionCacheUnavailableException)
        {
            // O SQL já forneceu a decisão atual. A próxima requisição tentará o cache novamente.
        }

        return snapshot.Value.Permissions;
    }

    private async Task<(long Version, IReadOnlySet<string> Permissions)?> LoadFromSqlAsync(
        Guid userGuid,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Usuarios.AsNoTracking()
            .Where(x => x.Guid == userGuid && x.Ativo && !x.Bloqueado)
            .Select(x => new { x.Id, x.VersaoPermissoes })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var direct = await (
            from userPermission in dbContext.UsuariosPermissoes.AsNoTracking()
            join permission in dbContext.Permissoes.AsNoTracking()
                on userPermission.PermissaoId equals permission.Id
            where userPermission.UsuarioId == user.Id
                  && userPermission.Ativo
                  && permission.Ativo
            select new { permission.Codigo, userPermission.Concedida })
            .ToListAsync(cancellationToken);

        var fromProfiles = await (
            from userProfile in dbContext.UsuariosPerfis.AsNoTracking()
            join profile in dbContext.Perfis.AsNoTracking() on userProfile.PerfilId equals profile.Id
            join profilePermission in dbContext.PerfisPermissoes.AsNoTracking() on profile.Id equals profilePermission.PerfilId
            join permission in dbContext.Permissoes.AsNoTracking() on profilePermission.PermissaoId equals permission.Id
            where userProfile.UsuarioId == user.Id
                  && userProfile.Ativo
                  && profile.Ativo
                  && profilePermission.Ativo
                  && permission.Ativo
            select permission.Codigo)
            .Distinct()
            .ToListAsync(cancellationToken);

        var denied = direct.Where(x => !x.Concedida).Select(x => x.Codigo).ToHashSet(StringComparer.Ordinal);
        var effective = fromProfiles
            .Concat(direct.Where(x => x.Concedida).Select(x => x.Codigo))
            .Where(code => !denied.Contains(code))
            .ToHashSet(StringComparer.Ordinal);
        return (user.VersaoPermissoes, effective);
    }
}
