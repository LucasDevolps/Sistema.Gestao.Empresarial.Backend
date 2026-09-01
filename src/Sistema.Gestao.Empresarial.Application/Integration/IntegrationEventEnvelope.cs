using System.Text.Json;

namespace Sistema.Gestao.Empresarial.Application.Integration;

public sealed record IntegrationEventEnvelope(
    Guid EventId,
    Guid MessageId,
    string EventType,
    int EventVersion,
    Guid CorrelationId,
    string TraceId,
    DateTimeOffset OccurredAt,
    string Producer,
    JsonElement Data);

public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken);
}

public interface IIntegrationEventHandler
{
    bool CanHandle(string eventType, int eventVersion);
    Task HandleAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken);
}

public sealed class TransientTechnicalException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class PermanentTechnicalException(string message, Exception? innerException = null)
    : Exception(message, innerException);
