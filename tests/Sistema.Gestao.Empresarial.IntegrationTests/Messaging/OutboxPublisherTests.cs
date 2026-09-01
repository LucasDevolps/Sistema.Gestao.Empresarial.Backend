using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Application.Integration;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Messaging;
using Sistema.Gestao.Empresarial.Infrastructure.Observability;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.IntegrationTests.Authentication;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Messaging;

public sealed class OutboxPublisherTests
{
    [Fact]
    public async Task PublicaMensagemEPreservaHistoricoNaOutbox()
    {
        await using var fixture = await OutboxFixture.CreateAsync();

        var processed = await fixture.Dispatcher.DispatchBatchAsync("worker-a", CancellationToken.None);

        var persisted = await fixture.ReloadAsync();
        Assert.Equal(1, processed);
        Assert.Equal("PUBLICADA", persisted.Status);
        Assert.Equal(1, persisted.Tentativas);
        Assert.NotNull(persisted.PublicadoEm);
        Assert.Null(persisted.LockId);
        Assert.Equal([fixture.MessageId], fixture.Publisher.PublishedMessageIds);
    }

    [Fact]
    public async Task FalhaTecnicaAgendaRetryComOMesmoMessageId()
    {
        await using var fixture = await OutboxFixture.CreateAsync();
        fixture.Publisher.FailAfterPublishCount = 1;

        await fixture.Dispatcher.DispatchBatchAsync("worker-a", CancellationToken.None);
        var failed = await fixture.ReloadAsync();
        Assert.Equal("ERRO", failed.Status);
        Assert.NotNull(failed.ProximaTentativaEm);

        Assert.Equal(0, await fixture.Dispatcher.DispatchBatchAsync("worker-a", CancellationToken.None));
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, await fixture.Dispatcher.DispatchBatchAsync("worker-a", CancellationToken.None));

        var published = await fixture.ReloadAsync();
        Assert.Equal("PUBLICADA", published.Status);
        Assert.Equal(2, published.Tentativas);
        Assert.Equal([fixture.MessageId, fixture.MessageId], fixture.Publisher.PublishedMessageIds);
    }

    [Fact]
    public async Task PayloadInconsistenteEhFalhaPermanenteSemRetry()
    {
        await using var fixture = await OutboxFixture.CreateAsync(mismatchEnvelope: true);

        Assert.Equal(1, await fixture.Dispatcher.DispatchBatchAsync("worker-a", CancellationToken.None));
        fixture.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0, await fixture.Dispatcher.DispatchBatchAsync("worker-a", CancellationToken.None));

        var persisted = await fixture.ReloadAsync();
        Assert.Equal("ERRO_PERMANENTE", persisted.Status);
        Assert.Equal(1, persisted.Tentativas);
        Assert.Empty(fixture.Publisher.PublishedMessageIds);
    }

    [Fact]
    public async Task LeaseExpiradoPodeSerRecuperadoPorOutroWorker()
    {
        await using var fixture = await OutboxFixture.CreateAsync();

        var first = await fixture.Store.ClaimBatchAsync(
            1, "worker-a", fixture.Clock.GetUtcNow(), TimeSpan.FromSeconds(10), CancellationToken.None);
        var beforeExpiry = await fixture.Store.ClaimBatchAsync(
            1, "worker-b", fixture.Clock.GetUtcNow(), TimeSpan.FromSeconds(10), CancellationToken.None);
        fixture.Clock.Advance(TimeSpan.FromSeconds(11));
        var recovered = await fixture.Store.ClaimBatchAsync(
            1, "worker-b", fixture.Clock.GetUtcNow(), TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Single(first);
        Assert.Empty(beforeExpiry);
        Assert.Single(recovered);
        Assert.True(recovered[0].RecoveredExpiredLease);
        Assert.Equal(fixture.MessageId, recovered[0].MessageId);
        Assert.Equal(2, recovered[0].Attempt);
    }
}

internal sealed class OutboxFixture : IAsyncDisposable
{
    private OutboxFixture(
        AppDbContext db,
        ManualTimeProvider clock,
        FakeIntegrationEventPublisher publisher,
        OutboxStore store,
        OutboxDispatcher dispatcher,
        ServiceProvider serviceProvider,
        Guid messageId)
    {
        Db = db;
        Clock = clock;
        Publisher = publisher;
        Store = store;
        Dispatcher = dispatcher;
        ServiceProvider = serviceProvider;
        MessageId = messageId;
    }

    private AppDbContext Db { get; }
    public ManualTimeProvider Clock { get; }
    public FakeIntegrationEventPublisher Publisher { get; }
    public OutboxStore Store { get; }
    public OutboxDispatcher Dispatcher { get; }
    private ServiceProvider ServiceProvider { get; }
    public Guid MessageId { get; }

    public static async Task<OutboxFixture> CreateAsync(bool mismatchEnvelope = false)
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(dbOptions, clock);
        var messageId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope(
            eventId,
            mismatchEnvelope ? Guid.NewGuid() : messageId,
            "EventoTeste",
            1,
            correlationId,
            "trace-test",
            clock.GetUtcNow(),
            "tests",
            JsonDocument.Parse("{\"value\":1}").RootElement.Clone());
        db.OutboxMessages.Add(new OutboxMessage(
            Guid.NewGuid(), messageId, eventId, "EventoTeste", 1,
            JsonSerializer.Serialize(envelope), correlationId, "trace-test", "tests", clock.GetUtcNow()));
        await db.SaveChangesAsync();

        var serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var publisher = new FakeIntegrationEventPublisher();
        var store = new OutboxStore(db);
        var options = Options.Create(new OutboxOptions
        {
            BatchSize = 10,
            PollingIntervalSeconds = 1,
            LeaseSeconds = 10,
            MaximumRetryDelaySeconds = 30
        });
        var dispatcher = new OutboxDispatcher(
            store,
            publisher,
            options,
            new OutboxMetrics(serviceProvider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>()),
            clock,
            NullLogger<OutboxDispatcher>.Instance);
        return new OutboxFixture(db, clock, publisher, store, dispatcher, serviceProvider, messageId);
    }

    public async Task<OutboxMessage> ReloadAsync()
    {
        Db.ChangeTracker.Clear();
        return await Db.OutboxMessages.SingleAsync(message => message.MessageId == MessageId);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await ServiceProvider.DisposeAsync();
    }
}

internal sealed class FakeIntegrationEventPublisher : IIntegrationEventPublisher
{
    public int FailAfterPublishCount { get; set; }
    public List<Guid> PublishedMessageIds { get; } = [];

    public Task PublishAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken)
    {
        PublishedMessageIds.Add(message.MessageId);
        if (FailAfterPublishCount > 0)
        {
            FailAfterPublishCount--;
            throw new TimeoutException("Falha transitória simulada depois do envio.");
        }
        return Task.CompletedTask;
    }
}
