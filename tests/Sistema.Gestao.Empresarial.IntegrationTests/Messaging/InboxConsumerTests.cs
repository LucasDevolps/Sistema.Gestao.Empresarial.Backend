using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sistema.Gestao.Empresarial.Application.Integration;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Infrastructure.Messaging;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.IntegrationTests.Authentication;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Messaging;

public sealed class InboxConsumerTests
{
    [Fact]
    public async Task MensagemDuplicadaNaoRepeteEfeitoColateral()
    {
        await using var fixture = InboxFixture.Create();

        var first = await fixture.Processor.ProcessAsync(fixture.Message, InboxFixture.ConsumerName, CancellationToken.None);
        var duplicate = await fixture.Processor.ProcessAsync(fixture.Message, InboxFixture.ConsumerName, CancellationToken.None);

        Assert.Equal(InboxProcessingOutcome.Processed, first);
        Assert.Equal(InboxProcessingOutcome.Duplicate, duplicate);
        Assert.Equal(1, fixture.Handler.Invocations);
        var inbox = await fixture.ReloadInboxAsync();
        Assert.Equal(InboxStatus.Processada, inbox.Status);
        Assert.Equal(1, inbox.Tentativas);
        Assert.Single(await fixture.Db.MessageAuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ExcecaoDeNegocioEhAuditadaComAckSemRetryOuDlq()
    {
        await using var fixture = InboxFixture.Create(new DomainException("Operação não permitida."));

        var outcome = await fixture.Processor.ProcessAsync(fixture.Message, InboxFixture.ConsumerName, CancellationToken.None);

        var inbox = await fixture.ReloadInboxAsync();
        Assert.Equal(InboxProcessingOutcome.RejectedBusinessRule, outcome);
        Assert.Equal(InboxStatus.RejeitadaRegraNegocio, inbox.Status);
        Assert.Equal(1, inbox.Tentativas);
        Assert.Null(inbox.Erro);
        Assert.Equal(InboxStatus.RejeitadaRegraNegocio, (await fixture.Db.MessageAuditLogs.SingleAsync()).Status);
    }

    [Fact]
    public async Task ExcecaoDeValidacaoEhAuditadaComAckSemRetryOuDlq()
    {
        await using var fixture = InboxFixture.Create(new ValidationException("Envelope semanticamente inválido."));

        var outcome = await fixture.Processor.ProcessAsync(fixture.Message, InboxFixture.ConsumerName, CancellationToken.None);

        var inbox = await fixture.ReloadInboxAsync();
        Assert.Equal(InboxProcessingOutcome.RejectedValidation, outcome);
        Assert.Equal(InboxStatus.RejeitadaValidacao, inbox.Status);
        Assert.Equal(1, inbox.Tentativas);
        Assert.Null(inbox.Erro);
    }

    [Fact]
    public async Task FalhaTransitoriaEhPersistidaEPodeSerReprocessada()
    {
        await using var fixture = InboxFixture.Create(new TransientTechnicalException("SQL indisponível."));

        var exception = await Assert.ThrowsAsync<TransientTechnicalException>(() =>
            fixture.Processor.ProcessAsync(fixture.Message, InboxFixture.ConsumerName, CancellationToken.None));
        await fixture.FailureRecorder.RecordAsync(
            fixture.Message,
            InboxFixture.ConsumerName,
            exception,
            false,
            CancellationToken.None);

        var failed = await fixture.ReloadInboxAsync();
        Assert.Equal(InboxStatus.Erro, failed.Status);
        Assert.Equal(1, failed.Tentativas);

        var outcome = await fixture.Processor.ProcessAsync(fixture.Message, InboxFixture.ConsumerName, CancellationToken.None);
        var processed = await fixture.ReloadInboxAsync();
        Assert.Equal(InboxProcessingOutcome.Processed, outcome);
        Assert.Equal(InboxStatus.Processada, processed.Status);
        Assert.Equal(2, processed.Tentativas);
        Assert.Equal([InboxStatus.Erro, InboxStatus.Processada],
            await fixture.Db.MessageAuditLogs.OrderBy(item => item.Id).Select(item => item.Status).ToListAsync());
    }

    [Fact]
    public async Task FalhaPermanenteEhPersistidaComoDlqENaoReprocessada()
    {
        await using var fixture = InboxFixture.Create(new PermanentTechnicalException("Contrato incompatível."));

        var exception = await Assert.ThrowsAsync<PermanentTechnicalException>(() =>
            fixture.Processor.ProcessAsync(fixture.Message, InboxFixture.ConsumerName, CancellationToken.None));
        await fixture.FailureRecorder.RecordAsync(
            fixture.Message,
            InboxFixture.ConsumerName,
            exception,
            true,
            CancellationToken.None);

        var duplicate = await fixture.Processor.ProcessAsync(fixture.Message, InboxFixture.ConsumerName, CancellationToken.None);
        var inbox = await fixture.ReloadInboxAsync();
        Assert.Equal(InboxProcessingOutcome.Duplicate, duplicate);
        Assert.Equal(InboxStatus.Dlq, inbox.Status);
        Assert.Equal(1, inbox.Tentativas);
        Assert.Single(await fixture.Db.MessageAuditLogs.Where(item => item.Status == InboxStatus.Dlq).ToListAsync());
    }
}

internal sealed class InboxFixture : IAsyncDisposable
{
    private InboxFixture(
        AppDbContext db,
        ManualTimeProvider clock,
        ConfigurableIntegrationEventHandler handler,
        InboxProcessor processor,
        InboxFailureRecorder failureRecorder,
        ServiceProvider serviceProvider,
        IntegrationEventEnvelope message)
    {
        Db = db;
        Clock = clock;
        Handler = handler;
        Processor = processor;
        FailureRecorder = failureRecorder;
        ServiceProvider = serviceProvider;
        Message = message;
    }

    public const string ConsumerName = "InboxConsumerTests";
    public AppDbContext Db { get; }
    public ManualTimeProvider Clock { get; }
    public ConfigurableIntegrationEventHandler Handler { get; }
    public InboxProcessor Processor { get; }
    public InboxFailureRecorder FailureRecorder { get; }
    private ServiceProvider ServiceProvider { get; }
    public IntegrationEventEnvelope Message { get; }

    public static InboxFixture Create(Exception? firstException = null)
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 1, 15, 0, 0, TimeSpan.Zero));
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options,
            clock);
        var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var metrics = new InboxMetrics(services.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
        var handler = new ConfigurableIntegrationEventHandler(firstException);
        var processor = new InboxProcessor(
            db,
            [handler],
            metrics,
            clock,
            NullLogger<InboxProcessor>.Instance);
        var failureRecorder = new InboxFailureRecorder(
            db,
            metrics,
            clock,
            NullLogger<InboxFailureRecorder>.Instance);
        var message = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "EventoTeste",
            1,
            Guid.NewGuid(),
            "0123456789abcdef0123456789abcdef",
            clock.GetUtcNow(),
            "tests",
            JsonDocument.Parse("{\"value\":1}").RootElement.Clone());
        return new InboxFixture(db, clock, handler, processor, failureRecorder, services, message);
    }

    public async Task<InboxMessage> ReloadInboxAsync()
    {
        Db.ChangeTracker.Clear();
        return await Db.InboxMessages.SingleAsync(
            item => item.MessageId == Message.MessageId && item.Consumer == ConsumerName);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await ServiceProvider.DisposeAsync();
    }
}

internal sealed class ConfigurableIntegrationEventHandler(Exception? firstException) : IIntegrationEventHandler
{
    private Exception? _nextException = firstException;
    public int Invocations { get; private set; }

    public bool CanHandle(string eventType, int eventVersion) => true;

    public Task HandleAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken)
    {
        Invocations++;
        if (_nextException is not null)
        {
            var exception = _nextException;
            _nextException = null;
            throw exception;
        }

        return Task.CompletedTask;
    }
}
