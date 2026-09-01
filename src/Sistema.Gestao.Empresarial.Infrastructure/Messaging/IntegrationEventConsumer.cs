using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Application.Integration;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;

namespace Sistema.Gestao.Empresarial.Infrastructure.Messaging;

public sealed class IntegrationEventConsumer(
    IInboxProcessor processor,
    IInboxFailureRecorder failureRecorder,
    IOptions<InboxOptions> options,
    ILogger<IntegrationEventConsumer> logger) : IConsumer<IntegrationEventEnvelope>
{
    public async Task Consume(ConsumeContext<IntegrationEventEnvelope> context)
    {
        try
        {
            await processor.ProcessAsync(
                context.Message,
                options.Value.ConsumerName,
                context.CancellationToken);
        }
        catch (TransientTechnicalException exception)
        {
            var finalAttempt = context.GetRetryAttempt() >= options.Value.TransientRetryCount;
            await failureRecorder.RecordAsync(
                context.Message,
                options.Value.ConsumerName,
                exception,
                finalAttempt,
                context.CancellationToken);
            throw;
        }
        catch (PermanentTechnicalException exception)
        {
            await failureRecorder.RecordAsync(
                context.Message,
                options.Value.ConsumerName,
                exception,
                true,
                context.CancellationToken);
            throw;
        }
        catch (Exception exception)
        {
            await failureRecorder.RecordAsync(
                context.Message,
                options.Value.ConsumerName,
                exception,
                true,
                context.CancellationToken);
            logger.LogError(
                exception,
                "Falha técnica não classificada no consumo da mensagem {MessageId}",
                context.Message.MessageId);
            throw;
        }
    }
}

public sealed class AuditOnlyIntegrationEventHandler : IIntegrationEventHandler
{
    public bool CanHandle(string eventType, int eventVersion) =>
        !string.IsNullOrWhiteSpace(eventType) && eventVersion > 0;

    public Task HandleAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
