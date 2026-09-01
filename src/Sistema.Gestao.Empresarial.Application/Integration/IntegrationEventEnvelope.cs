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
