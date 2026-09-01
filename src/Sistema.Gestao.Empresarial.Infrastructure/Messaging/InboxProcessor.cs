using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Sistema.Gestao.Empresarial.Application.Integration;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Messaging;

public enum InboxProcessingOutcome
{
    Processed,
    Duplicate,
    RejectedBusinessRule,
    RejectedValidation
}

public interface IInboxProcessor
{
    Task<InboxProcessingOutcome> ProcessAsync(
        IntegrationEventEnvelope message,
        string consumer,
        CancellationToken cancellationToken);
}

public interface IInboxFailureRecorder
{
    Task RecordAsync(
        IntegrationEventEnvelope message,
        string consumer,
        Exception exception,
        bool sendToDlq,
        CancellationToken cancellationToken);
}

public sealed class InboxProcessor(
    AppDbContext dbContext,
    IEnumerable<IIntegrationEventHandler> handlers,
    InboxMetrics metrics,
    TimeProvider timeProvider,
    ILogger<InboxProcessor> logger) : IInboxProcessor
{
    public async Task<InboxProcessingOutcome> ProcessAsync(
        IntegrationEventEnvelope message,
        string consumer,
        CancellationToken cancellationToken)
    {
        ValidateEnvelope(message);
        var inbox = await GetOrCreateAsync(message, consumer, cancellationToken);
        if (inbox.Finalizada)
        {
            return RegisterDuplicate(message.MessageId, consumer);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() =>
            ProcessWithLockAsync(message, consumer, cancellationToken));
    }

    private async Task<InboxProcessingOutcome> ProcessWithLockAsync(
        IntegrationEventEnvelope message,
        string consumer,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);
        InboxMessage? inbox = null;
        try
        {
            inbox = await LoadForProcessingAsync(message.MessageId, consumer, cancellationToken);
            if (inbox.Finalizada)
            {
                await CommitAsync(transaction, cancellationToken);
                return RegisterDuplicate(message.MessageId, consumer);
            }

            var now = timeProvider.GetUtcNow();
            inbox.IniciarTentativa(now);
            var handler = handlers.FirstOrDefault(candidate => candidate.CanHandle(message.EventType, message.EventVersion))
                ?? throw new PermanentTechnicalException(
                    $"Nenhum handler foi registrado para {message.EventType} v{message.EventVersion}.");

            await handler.HandleAsync(message, cancellationToken);
            inbox.MarcarProcessada(timeProvider.GetUtcNow());
            AddAudit(inbox, InboxStatus.Processada, null, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            metrics.Processed();
            return InboxProcessingOutcome.Processed;
        }
        catch (DomainException exception)
        {
            return await RejectAsync(
                inbox!,
                InboxStatus.RejeitadaRegraNegocio,
                InboxProcessingOutcome.RejectedBusinessRule,
                exception,
                transaction,
                cancellationToken);
        }
        catch (ValidationException exception)
        {
            return await RejectAsync(
                inbox!,
                InboxStatus.RejeitadaValidacao,
                InboxProcessingOutcome.RejectedValidation,
                exception,
                transaction,
                cancellationToken);
        }
        catch
        {
            await RollbackAsync(transaction, cancellationToken);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<InboxProcessingOutcome> RejectAsync(
        InboxMessage inbox,
        string status,
        InboxProcessingOutcome outcome,
        Exception exception,
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        inbox.MarcarRejeitada(status, exception.Message, now);
        AddAudit(inbox, status, exception.Message, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        metrics.Rejected(status);
        logger.LogWarning(
            exception,
            "Mensagem {MessageId} rejeitada com status {Status}; ACK será enviado sem retry",
            inbox.MessageId,
            status);
        return outcome;
    }

    private async Task<InboxMessage> GetOrCreateAsync(
        IntegrationEventEnvelope message,
        string consumer,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.InboxMessages.SingleOrDefaultAsync(
            item => item.MessageId == message.MessageId && item.Consumer == consumer,
            cancellationToken);
        if (existing is not null)
            return existing;

        var created = new InboxMessage(
            Guid.NewGuid(),
            message.MessageId,
            consumer,
            message.EventType,
            JsonSerializer.Serialize(message),
            message.CorrelationId,
            message.TraceId,
            timeProvider.GetUtcNow());
        dbContext.InboxMessages.Add(created);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            metrics.Received();
            return created;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            return await dbContext.InboxMessages.SingleAsync(
                item => item.MessageId == message.MessageId && item.Consumer == consumer,
                cancellationToken);
        }
    }

    private Task<InboxMessage> LoadForProcessingAsync(
        Guid messageId,
        string consumer,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            return dbContext.InboxMessages.SingleAsync(
                item => item.MessageId == messageId && item.Consumer == consumer,
                cancellationToken);
        }

        return dbContext.InboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM [sge].[InboxMessages] WITH (UPDLOCK, ROWLOCK)
                WHERE [MessageId] = {messageId}
                  AND [Consumer] = {consumer}
                  AND [Excluido] = CAST(0 AS bit)
                """)
            .SingleAsync(cancellationToken);
    }

    private InboxProcessingOutcome RegisterDuplicate(Guid messageId, string consumer)
    {
        metrics.Duplicate();
        logger.LogInformation(
            "Mensagem duplicada {MessageId} ignorada pelo consumer {Consumer}",
            messageId,
            consumer);
        return InboxProcessingOutcome.Duplicate;
    }

    private void AddAudit(InboxMessage inbox, string status, string? detail, DateTimeOffset now) =>
        dbContext.MessageAuditLogs.Add(new MessageAuditLog(
            Guid.NewGuid(),
            inbox.MessageId,
            inbox.EventType,
            inbox.Consumer,
            status,
            inbox.Tentativas,
            inbox.CorrelationId,
            inbox.TraceId,
            now,
            detail));

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    private static Task RollbackAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.RollbackAsync(cancellationToken) ?? Task.CompletedTask;

    private static void ValidateEnvelope(IntegrationEventEnvelope message)
    {
        if (message.MessageId == Guid.Empty
            || message.EventId == Guid.Empty
            || message.CorrelationId == Guid.Empty
            || string.IsNullOrWhiteSpace(message.EventType)
            || message.EventVersion <= 0
            || string.IsNullOrWhiteSpace(message.TraceId))
        {
            throw new PermanentTechnicalException("O envelope recebido contém metadados obrigatórios inválidos.");
        }
    }
}

public sealed class InboxFailureRecorder(
    AppDbContext dbContext,
    InboxMetrics metrics,
    TimeProvider timeProvider,
    ILogger<InboxFailureRecorder> logger) : IInboxFailureRecorder
{
    public async Task RecordAsync(
        IntegrationEventEnvelope message,
        string consumer,
        Exception exception,
        bool sendToDlq,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var inbox = await dbContext.InboxMessages.SingleOrDefaultAsync(
            item => item.MessageId == message.MessageId && item.Consumer == consumer,
            cancellationToken);
        if (inbox?.Finalizada == true)
            return;

        inbox ??= CreateInbox(message, consumer, now);
        if (inbox.Id == 0 && dbContext.Entry(inbox).State == EntityState.Detached)
            dbContext.InboxMessages.Add(inbox);

        inbox.RegistrarFalhaTecnica(
            $"{exception.GetType().FullName}: {exception.Message}",
            sendToDlq,
            now);
        dbContext.MessageAuditLogs.Add(new MessageAuditLog(
            Guid.NewGuid(),
            message.MessageId,
            message.EventType,
            consumer,
            sendToDlq ? InboxStatus.Dlq : InboxStatus.Erro,
            inbox.Tentativas,
            message.CorrelationId,
            message.TraceId,
            now,
            exception.Message));
        await dbContext.SaveChangesAsync(cancellationToken);

        if (sendToDlq)
        {
            metrics.SentToDlq();
            logger.LogError(exception, "Mensagem {MessageId} marcada como DLQ", message.MessageId);
        }
        else
        {
            metrics.Retry();
            logger.LogWarning(exception, "Mensagem {MessageId} aguardará retry técnico", message.MessageId);
        }
    }

    private static InboxMessage CreateInbox(
        IntegrationEventEnvelope message,
        string consumer,
        DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            message.MessageId,
            consumer,
            message.EventType,
            JsonSerializer.Serialize(message),
            message.CorrelationId,
            message.TraceId,
            now);
}
