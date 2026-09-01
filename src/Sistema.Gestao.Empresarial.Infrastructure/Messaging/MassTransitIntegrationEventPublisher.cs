using MassTransit;
using Sistema.Gestao.Empresarial.Application.Integration;

namespace Sistema.Gestao.Empresarial.Infrastructure.Messaging;

public sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    : IIntegrationEventPublisher
{
    public Task PublishAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(
            message,
            context =>
            {
                context.MessageId = message.MessageId;
                context.CorrelationId = message.CorrelationId;
                context.Headers.Set("eventId", message.EventId);
                context.Headers.Set("eventType", message.EventType);
                context.Headers.Set("eventVersion", message.EventVersion);
                context.Headers.Set("traceId", message.TraceId);
                context.Headers.Set("producer", message.Producer);
            },
            cancellationToken);
}
