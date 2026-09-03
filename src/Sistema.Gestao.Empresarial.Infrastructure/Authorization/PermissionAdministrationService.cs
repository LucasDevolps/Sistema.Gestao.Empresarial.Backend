using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Authorization;

public sealed class PermissionAdministrationService(
    AppDbContext dbContext,
    IPermissionChecker permissionChecker,
    IPermissionCache permissionCache,
    PermissionMetrics metrics,
    TimeProvider timeProvider) : IPermissionAdministrationService
{
    private const string Producer = "Sistema.Gestao.Empresarial.Api";
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> InProcessLocks = new();

    public async Task<UserPermissionsResponse?> GetAsync(
        Guid actorUserGuid,
        Guid userGuid,
        CancellationToken cancellationToken)
    {
        var actorOrganizationId = await GetOrganizationIdAsync(actorUserGuid, cancellationToken);
        if (!actorOrganizationId.HasValue)
        {
            return null;
        }

        var user = await dbContext.Usuarios.AsNoTracking()
            .Where(x => x.Guid == userGuid
                && x.Ativo
                && x.FuncionarioId.HasValue
                && x.Funcionario!.UnidadeContratacao.OrganizacaoId == actorOrganizationId.Value)
            .Select(x => new { x.Guid, x.VersaoPermissoes })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var permissions = await permissionChecker.GetPermissionsAsync(userGuid, cancellationToken);
        return new UserPermissionsResponse(user.Guid, user.VersaoPermissoes, [.. permissions.Order()]);
    }

    public async Task<PermissionChangeResult> SetDirectPermissionAsync(
        Guid actorUserGuid,
        Guid targetUserGuid,
        string permissionCode,
        bool granted,
        Guid correlationId,
        string traceId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (actorUserGuid == Guid.Empty || actorUserGuid == targetUserGuid)
        {
            return PermissionChangeResult.Forbidden;
        }

        var normalizedCode = permissionCode.Trim().ToUpperInvariant();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            if (Interlocked.Increment(ref attempt) > 1)
            {
                // Uma tentativa anterior pode ter deixado entidades rastreadas após falha transitória.
                dbContext.ChangeTracker.Clear();
            }

            var identities = await dbContext.Usuarios.AsNoTracking()
                .Where(x => x.Guid == actorUserGuid || x.Guid == targetUserGuid)
                .Select(x => new
                {
                    x.Id,
                    x.Guid,
                    OrganizationId = x.FuncionarioId.HasValue
                        ? (long?)x.Funcionario!.UnidadeContratacao.OrganizacaoId
                        : null
                })
                .ToListAsync(cancellationToken);
            var actorIdentity = identities.SingleOrDefault(x => x.Guid == actorUserGuid);
            var targetIdentity = identities.SingleOrDefault(x => x.Guid == targetUserGuid);
            if (actorIdentity is null)
            {
                return PermissionChangeResult.Forbidden;
            }
            if (targetIdentity is null)
            {
                return PermissionChangeResult.UserNotFound;
            }
            if (!actorIdentity.OrganizationId.HasValue
                || actorIdentity.OrganizationId != targetIdentity.OrganizationId)
            {
                return PermissionChangeResult.Forbidden;
            }

            await using var transaction = await BeginTransactionAsync(cancellationToken);
            await using var locks = await AcquireUserLocksAsync(
                [actorIdentity.Id, targetIdentity.Id], cancellationToken);

            if (!await permissionChecker.HasPermissionAsync(
                    actorUserGuid, PermissionCodes.ManageUserPermissions, cancellationToken))
            {
                return PermissionChangeResult.Forbidden;
            }
            if (granted && !await permissionChecker.HasPermissionAsync(
                    actorUserGuid, normalizedCode, cancellationToken))
            {
                return PermissionChangeResult.Forbidden;
            }

            var target = await dbContext.Usuarios.SingleOrDefaultAsync(
                x => x.Id == targetIdentity.Id && x.Ativo, cancellationToken);
            if (target is null)
            {
                return PermissionChangeResult.UserNotFound;
            }
            var permission = await dbContext.Permissoes.SingleOrDefaultAsync(
                x => x.Codigo == normalizedCode && x.Ativo, cancellationToken);
            if (permission is null)
            {
                return PermissionChangeResult.PermissionNotFound;
            }

            var direct = await dbContext.UsuariosPermissoes.SingleOrDefaultAsync(
                x => x.UsuarioId == target.Id && x.PermissaoId == permission.Id,
                cancellationToken);
            var now = timeProvider.GetUtcNow();
            var changed = direct is null;
            if (direct is null)
            {
                direct = new UsuarioPermissao(Guid.NewGuid(), target.Id, permission.Id, granted, now);
                dbContext.UsuariosPermissoes.Add(direct);
            }
            else
            {
                changed = direct.AlterarConcessao(granted, now);
            }

            if (!changed)
            {
                return PermissionChangeResult.Unchanged;
            }

            var version = target.IncrementarVersaoPermissoes(now);
            AddAuditAndOutbox(
                actorUserGuid,
                targetUserGuid,
                normalizedCode,
                granted,
                correlationId,
                traceId,
                ipAddress,
                now);

            // A barreira vem antes do commit: nenhuma réplica pode recarregar a versão antiga
            // durante a janela entre invalidação e confirmação no SQL.
            await permissionCache.AdvanceVersionAsync(targetUserGuid, version, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            metrics.Invalidated();
            return PermissionChangeResult.Changed;
        });
    }

    private Task<long?> GetOrganizationIdAsync(Guid userGuid, CancellationToken cancellationToken) =>
        dbContext.Usuarios.AsNoTracking()
            .Where(x => x.Guid == userGuid && x.Ativo && x.FuncionarioId.HasValue)
            .Select(x => (long?)x.Funcionario!.UnidadeContratacao.OrganizacaoId)
            .SingleOrDefaultAsync(cancellationToken);

    private void AddAuditAndOutbox(
        Guid actorUserGuid,
        Guid targetUserGuid,
        string permissionCode,
        bool granted,
        Guid correlationId,
        string traceId,
        string? ipAddress,
        DateTimeOffset now)
    {
        var action = granted ? "PERMISSAO_CONCEDIDA" : "PERMISSAO_NEGADA";
        var data = new { targetUserGuid, permissionCode, granted };
        dbContext.AuditLogs.Add(new AuditLog(
            Guid.NewGuid(), "UsuarioPermissao", targetUserGuid, action, actorUserGuid,
            now, correlationId, traceId, ipAddress, null, JsonSerializer.Serialize(data)));

        var eventId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var envelope = new
        {
            eventId,
            messageId,
            eventType = "UsuarioPermissaoAlterada",
            eventVersion = 1,
            correlationId,
            traceId,
            occurredAt = now,
            producer = Producer,
            data
        };
        dbContext.OutboxMessages.Add(new OutboxMessage(
            Guid.NewGuid(), messageId, eventId, "UsuarioPermissaoAlterada", 1,
            JsonSerializer.Serialize(envelope), correlationId, traceId, Producer, now));
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

    private async Task<IAsyncDisposable> AcquireUserLocksAsync(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken)
    {
        var orderedIds = userIds.Distinct().Order().ToArray();
        if (!dbContext.Database.IsSqlServer())
        {
            var acquired = new List<SemaphoreSlim>(orderedIds.Length);
            try
            {
                foreach (var userId in orderedIds)
                {
                    var semaphore = InProcessLocks.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));
                    await semaphore.WaitAsync(cancellationToken);
                    acquired.Add(semaphore);
                }
                return new SemaphoreCollectionReleaser(acquired);
            }
            catch
            {
                acquired.Reverse();
                acquired.ForEach(x => x.Release());
                throw;
            }
        }

        var connection = dbContext.Database.GetDbConnection();
        foreach (var userId in orderedIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
                SELECT @result;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@resource";
            parameter.Value = $"sge:permissions:user:{userId.ToString(CultureInfo.InvariantCulture)}";
            command.Parameters.Add(parameter);
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            if (result < 0)
            {
                throw new TimeoutException("Não foi possível adquirir o lock de permissões.");
            }
        }
        return NoopAsyncDisposable.Instance;
    }

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    private sealed class SemaphoreCollectionReleaser(IReadOnlyList<SemaphoreSlim> semaphores) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            for (var index = semaphores.Count - 1; index >= 0; index--)
            {
                semaphores[index].Release();
            }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoopAsyncDisposable Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
