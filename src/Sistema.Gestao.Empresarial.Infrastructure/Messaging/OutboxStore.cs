using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Messaging;

public sealed record OutboxClaim(
    Guid MessageId,
    Guid EventId,
    string EventType,
    int EventVersion,
    string Payload,
    Guid CorrelationId,
    string TraceId,
    string Producer,
    DateTimeOffset OccurredAt,
    Guid LockId,
    int Attempt,
    bool RecoveredExpiredLease);

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxClaim>> ClaimBatchAsync(
        int batchSize,
        string workerId,
        DateTimeOffset now,
        TimeSpan lease,
        CancellationToken cancellationToken);

    Task MarkPublishedAsync(Guid messageId, Guid lockId, DateTimeOffset now, CancellationToken cancellationToken);
    Task MarkTransientFailureAsync(
        Guid messageId,
        Guid lockId,
        string error,
        DateTimeOffset now,
        DateTimeOffset nextAttempt,
        CancellationToken cancellationToken);
    Task MarkPermanentFailureAsync(
        Guid messageId,
        Guid lockId,
        string error,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed class OutboxStore(AppDbContext dbContext) : IOutboxStore
{
    private static readonly SemaphoreSlim NonRelationalClaimLock = new(1, 1);

    public async Task<IReadOnlyList<OutboxClaim>> ClaimBatchAsync(
        int batchSize,
        string workerId,
        DateTimeOffset now,
        TimeSpan lease,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
            return await ClaimNonRelationalAsync(batchSize, workerId, now, lease, cancellationToken);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync<IReadOnlyList<OutboxClaim>>(async () =>
        {
            if (Interlocked.Increment(ref attempt) > 1)
                dbContext.ChangeTracker.Clear();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var ids = await dbContext.Database.SqlQuery<long>($"""
                    SELECT TOP ({batchSize}) [Id] AS [Value]
                    FROM [sge].[OutboxMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE [Ativo] = CAST(1 AS bit)
                      AND [Excluido] = CAST(0 AS bit)
                      AND (
                          ([Status] IN (N'PENDENTE', N'ERRO')
                              AND ([ProximaTentativaEm] IS NULL OR [ProximaTentativaEm] <= {now}))
                          OR ([Status] = N'PROCESSANDO' AND [BloqueadoAte] <= {now})
                      )
                    ORDER BY [OccurredAt], [Id]
                    """)
                .ToListAsync(cancellationToken);
            if (ids.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return Array.Empty<OutboxClaim>();
            }

            var messages = await dbContext.OutboxMessages
                .Where(message => ids.Contains(message.Id))
                .OrderBy(message => message.OccurredAt)
                .ThenBy(message => message.Id)
                .ToListAsync(cancellationToken);
            var claims = new List<OutboxClaim>(messages.Count);
            foreach (var message in messages)
            {
                var recovered = message.Status == "PROCESSANDO";
                var lockId = Guid.NewGuid();
                message.Reivindicar(lockId, workerId, now, lease);
                claims.Add(ToClaim(message, lockId, recovered));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return claims;
        });
    }

    public async Task MarkPublishedAsync(
        Guid messageId,
        Guid lockId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = await FindClaimedAsync(messageId, cancellationToken);
        message.MarcarPublicada(lockId, now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkTransientFailureAsync(
        Guid messageId,
        Guid lockId,
        string error,
        DateTimeOffset now,
        DateTimeOffset nextAttempt,
        CancellationToken cancellationToken)
    {
        var message = await FindClaimedAsync(messageId, cancellationToken);
        message.RegistrarFalhaTransitoria(lockId, error, now, nextAttempt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPermanentFailureAsync(
        Guid messageId,
        Guid lockId,
        string error,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = await FindClaimedAsync(messageId, cancellationToken);
        message.RegistrarFalhaPermanente(lockId, error, now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<OutboxClaim>> ClaimNonRelationalAsync(
        int batchSize,
        string workerId,
        DateTimeOffset now,
        TimeSpan lease,
        CancellationToken cancellationToken)
    {
        await NonRelationalClaimLock.WaitAsync(cancellationToken);
        try
        {
            var messages = await dbContext.OutboxMessages
                .Where(message =>
                    ((message.Status == "PENDENTE" || message.Status == "ERRO")
                        && (!message.ProximaTentativaEm.HasValue || message.ProximaTentativaEm <= now))
                    || (message.Status == "PROCESSANDO" && message.BloqueadoAte <= now))
                .OrderBy(message => message.OccurredAt)
                .ThenBy(message => message.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            var claims = new List<OutboxClaim>(messages.Count);
            foreach (var message in messages)
            {
                var recovered = message.Status == "PROCESSANDO";
                var lockId = Guid.NewGuid();
                message.Reivindicar(lockId, workerId, now, lease);
                claims.Add(ToClaim(message, lockId, recovered));
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            return claims;
        }
        finally
        {
            NonRelationalClaimLock.Release();
        }
    }

    private async Task<OutboxMessage> FindClaimedAsync(Guid messageId, CancellationToken cancellationToken) =>
        await dbContext.OutboxMessages.SingleAsync(message => message.MessageId == messageId, cancellationToken);

    private static OutboxClaim ToClaim(OutboxMessage message, Guid lockId, bool recovered) =>
        new(
            message.MessageId,
            message.EventId,
            message.EventType,
            message.EventVersion,
            message.Payload,
            message.CorrelationId,
            message.TraceId,
            message.Producer,
            message.OccurredAt,
            lockId,
            message.Tentativas,
            recovered);
}
