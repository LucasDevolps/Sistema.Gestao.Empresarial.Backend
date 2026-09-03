using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sistema.Gestao.Empresarial.Application.Integration;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Infrastructure.Messaging;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed class RabbitMqInboxTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task RabbitMqEInbox_DevemDeduplicarEConfirmarRejeicaoDeNegocio()
    {
        const string consumer = "RealInfrastructureInboxConsumer";
        var handledEffects = 0;
        var deliveries = 0;
        var allDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var metrics = new InboxMetrics(
            fixture.Metrics.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
        var handler = new TestIntegrationHandler(() => Interlocked.Increment(ref handledEffects));
        var queueName = $"sge-it-{fixture.IsolationKey}";
        var bus = Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host(
                fixture.RabbitMqHost,
                fixture.RabbitMqPort,
                fixture.RabbitMqVirtualHost,
                host =>
                {
                    host.Username(fixture.RabbitMqUsername);
                    host.Password(fixture.RabbitMqPassword);
                });
            configuration.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Durable = false;
                endpoint.AutoDelete = true;
                endpoint.ConcurrentMessageLimit = 4;
                endpoint.Handler<IntegrationEventEnvelope>(async context =>
                {
                    await using var db = fixture.CreateDbContext();
                    var processor = new InboxProcessor(
                        db,
                        [handler],
                        metrics,
                        TimeProvider.System,
                        NullLogger<InboxProcessor>.Instance);
                    await processor.ProcessAsync(context.Message, consumer, context.CancellationToken);
                    if (Interlocked.Increment(ref deliveries) == 3)
                    {
                        allDelivered.TrySetResult();
                    }
                });
            });
        });

        var duplicate = Message("RealIntegrationProcessed");
        var rejected = Message("RealIntegrationBusinessRejected");
        await bus.StartAsync();
        try
        {
            await bus.Publish(duplicate);
            await bus.Publish(duplicate);
            await bus.Publish(rejected);
            await allDelivered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            await bus.StopAsync();
        }

        Assert.Equal(1, handledEffects);
        await using var verification = fixture.CreateDbContext();
        var processedInbox = await verification.InboxMessages
            .SingleAsync(x => x.MessageId == duplicate.MessageId && x.Consumer == consumer);
        var rejectedInbox = await verification.InboxMessages
            .SingleAsync(x => x.MessageId == rejected.MessageId && x.Consumer == consumer);
        Assert.Equal(InboxStatus.Processada, processedInbox.Status);
        Assert.Equal(1, processedInbox.Tentativas);
        Assert.Equal(InboxStatus.RejeitadaRegraNegocio, rejectedInbox.Status);
        Assert.Equal(1, rejectedInbox.Tentativas);
        Assert.Equal(
            2,
            await verification.MessageAuditLogs.CountAsync(x =>
                x.Consumer == consumer
                && (x.MessageId == duplicate.MessageId || x.MessageId == rejected.MessageId)));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task FalhasTecnicas_DevemPersistirRetryEDlqSemApagarHistorico()
    {
        const string consumer = "RealInfrastructureFailureRecorder";
        var message = Message("RealIntegrationTechnicalFailure");
        var metrics = new InboxMetrics(
            fixture.Metrics.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        await using (var retryDb = fixture.CreateDbContext())
        {
            var recorder = new InboxFailureRecorder(
                retryDb, metrics, TimeProvider.System, NullLogger<InboxFailureRecorder>.Instance);
            await recorder.RecordAsync(
                message,
                consumer,
                new TransientTechnicalException("Falha transitória induzida."),
                false,
                CancellationToken.None);
        }

        await using (var dlqDb = fixture.CreateDbContext())
        {
            var recorder = new InboxFailureRecorder(
                dlqDb, metrics, TimeProvider.System, NullLogger<InboxFailureRecorder>.Instance);
            await recorder.RecordAsync(
                message,
                consumer,
                new PermanentTechnicalException("Falha permanente induzida."),
                true,
                CancellationToken.None);
        }

        await using var verification = fixture.CreateDbContext();
        var inbox = await verification.InboxMessages
            .SingleAsync(x => x.MessageId == message.MessageId && x.Consumer == consumer);
        Assert.Equal(InboxStatus.Dlq, inbox.Status);
        Assert.Equal(2, inbox.Tentativas);
        var auditStatuses = await verification.MessageAuditLogs
            .Where(x => x.MessageId == message.MessageId && x.Consumer == consumer)
            .OrderBy(x => x.Tentativa)
            .Select(x => x.Status)
            .ToListAsync();
        Assert.Equal([InboxStatus.Erro, InboxStatus.Dlq], auditStatuses);
    }

    private static IntegrationEventEnvelope Message(string eventType) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            eventType,
            1,
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            TimeProvider.System.GetUtcNow(),
            "Sistema.Gestao.Empresarial.RealIntegrationTests",
            JsonSerializer.SerializeToElement(new { test = true }));

    private sealed class TestIntegrationHandler(Action handled) : IIntegrationEventHandler
    {
        public bool CanHandle(string eventType, int eventVersion) =>
            eventType.StartsWith("RealIntegration", StringComparison.Ordinal) && eventVersion == 1;

        public Task HandleAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken)
        {
            if (message.EventType == "RealIntegrationBusinessRejected")
            {
                throw new DomainException("Regra de negócio rejeitada intencionalmente.");
            }

            handled();
            return Task.CompletedTask;
        }
    }
}
