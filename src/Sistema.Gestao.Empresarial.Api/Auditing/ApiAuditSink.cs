using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Api.Auditing;

public sealed record ApiAuditEntry(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Method,
    string Endpoint,
    int StatusCode,
    long ElapsedMilliseconds,
    Guid CorrelationId,
    string TraceId,
    Guid? UserGuid,
    string Environment,
    string? ExceptionType);

public interface IApiAuditSink
{
    bool TryWrite(ApiAuditEntry entry);
}

public sealed class ApiAuditSink : BackgroundService, IApiAuditSink
{
    private readonly Channel<ApiAuditEntry> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuditOptions _options;
    private readonly ILogger<ApiAuditSink> _logger;

    public ApiAuditSink(
        IServiceScopeFactory scopeFactory,
        IOptions<AuditOptions> options,
        ILogger<ApiAuditSink> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<ApiAuditEntry>(new BoundedChannelOptions(_options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool TryWrite(ApiAuditEntry entry)
    {
        var accepted = _channel.Writer.TryWrite(entry);
        if (!accepted)
        {
            _logger.LogWarning(
                "Evento de auditoria descartado porque a fila está cheia. Correlação {CorrelationId}",
                entry.CorrelationId);
        }
        return accepted;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await PersistAsync(entry, stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }

    private async Task PersistAsync(ApiAuditEntry entry, CancellationToken stoppingToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.PersistenceTimeoutSeconds));
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            long? userId = null;
            if (entry.UserGuid.HasValue)
            {
                userId = await dbContext.Usuarios.AsNoTracking()
                    .Where(x => x.Guid == entry.UserGuid.Value)
                    .Select(x => (long?)x.Id)
                    .SingleOrDefaultAsync(timeout.Token);
            }

            dbContext.ApiRequestLogs.Add(new ApiRequestLog(
                Guid.NewGuid(), entry.StartedAt, entry.FinishedAt, entry.Method, entry.Endpoint,
                null, "{}", null, "{}", null, entry.StatusCode, entry.ElapsedMilliseconds,
                entry.StatusCode is >= 200 and < 400, entry.CorrelationId, entry.TraceId,
                null, null, userId, entry.UserGuid, entry.Environment, entry.ExceptionType));
            await dbContext.SaveChangesAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(
                "Timeout ao persistir auditoria HTTP da correlação {CorrelationId}",
                entry.CorrelationId);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha ao persistir auditoria HTTP da correlação {CorrelationId}",
                entry.CorrelationId);
        }
    }
}
