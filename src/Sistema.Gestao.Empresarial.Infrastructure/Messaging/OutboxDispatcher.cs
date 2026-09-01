using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Application.Integration;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;

namespace Sistema.Gestao.Empresarial.Infrastructure.Messaging;

public interface IOutboxDispatcher
{
    Task<int> DispatchBatchAsync(string workerId, CancellationToken cancellationToken);
}

public sealed class OutboxDispatcher(
    IOutboxStore store,
    IIntegrationEventPublisher publisher,
    IOptions<OutboxOptions> options,
    OutboxMetrics metrics,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OutboxOptions _options = options.Value;

    public async Task<int> DispatchBatchAsync(string workerId, CancellationToken cancellationToken)
    {
        var claims = await store.ClaimBatchAsync(
            _options.BatchSize,
            workerId,
            timeProvider.GetUtcNow(),
            TimeSpan.FromSeconds(_options.LeaseSeconds),
            cancellationToken);
        metrics.Claimed(claims.Count);

        foreach (var claim in claims)
        {
            using var activity = OutboxMetrics.ActivitySource.StartActivity("outbox.publish", ActivityKind.Producer);
            activity?.SetTag("messaging.message.id", claim.MessageId);
            activity?.SetTag("messaging.operation.type", "publish");
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("sge.correlation_id", claim.CorrelationId);
            if (claim.RecoveredExpiredLease)
                metrics.RecoveredLease();

            try
            {
                var envelope = DeserializeAndValidate(claim);
                await publisher.PublishAsync(envelope, cancellationToken);
                var publishedAt = timeProvider.GetUtcNow();
                await store.MarkPublishedAsync(claim.MessageId, claim.LockId, publishedAt, cancellationToken);
                metrics.Published(publishedAt - claim.OccurredAt);
                logger.LogInformation(
                    "Mensagem {MessageId} do tipo {EventType} publicada na tentativa {Attempt}",
                    claim.MessageId,
                    claim.EventType,
                    claim.Attempt);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
                await RegisterFailureAsync(claim, exception, cancellationToken);
            }
        }

        return claims.Count;
    }

    private async Task RegisterFailureAsync(
        OutboxClaim claim,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var error = $"{exception.GetType().FullName}: {exception.Message}";
        if (exception is JsonException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            await store.MarkPermanentFailureAsync(claim.MessageId, claim.LockId, error, now, cancellationToken);
            metrics.PermanentFailure();
            logger.LogError(
                exception,
                "Mensagem {MessageId} rejeitada permanentemente antes da publicação",
                claim.MessageId);
            return;
        }

        var exponent = Math.Min(claim.Attempt - 1, 10);
        var delaySeconds = Math.Min(
            _options.MaximumRetryDelaySeconds,
            _options.PollingIntervalSeconds * Math.Pow(2, exponent));
        var nextAttempt = now.AddSeconds(delaySeconds);
        await store.MarkTransientFailureAsync(
            claim.MessageId,
            claim.LockId,
            error,
            now,
            nextAttempt,
            cancellationToken);
        metrics.TransientFailure();
        logger.LogWarning(
            exception,
            "Falha transitória ao publicar {MessageId}; nova tentativa em {NextAttempt}",
            claim.MessageId,
            nextAttempt);
    }

    private static IntegrationEventEnvelope DeserializeAndValidate(OutboxClaim claim)
    {
        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(claim.Payload, JsonOptions)
            ?? throw new InvalidDataException("O payload da Outbox não contém um envelope válido.");
        if (envelope.MessageId != claim.MessageId
            || envelope.EventId != claim.EventId
            || envelope.EventType != claim.EventType
            || envelope.EventVersion != claim.EventVersion
            || envelope.CorrelationId != claim.CorrelationId)
        {
            throw new InvalidDataException("O envelope não corresponde aos metadados persistidos na Outbox.");
        }
        return envelope;
    }
}
