using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Auditing;

public sealed class AuditRetentionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<AuditRetentionOptions> options,
    ILogger<AuditRetentionWorker> logger) : BackgroundService
{
    private readonly AuditRetentionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Expurgo de auditoria desabilitado por configuração.");
            return;
        }

        await SweepAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.SweepIntervalHours), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepAsync(stoppingToken);
        }
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var httpCutoff = now.AddDays(-_options.HttpAccessLogDays);
        var businessCutoff = now.AddDays(-_options.BusinessAuditDays);
        var httpDeleted = await DeleteHttpLogsAsync(httpCutoff, cancellationToken);
        var businessDeleted = await DeleteBusinessAuditsAsync(businessCutoff, cancellationToken);

        logger.LogInformation(
            "Retenção de auditoria concluída. Logs HTTP removidos: {HttpDeleted}; auditorias de negócio removidas: {BusinessDeleted}",
            httpDeleted,
            businessDeleted);
    }

    private async Task<int> DeleteHttpLogsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var total = 0;
        for (var batch = 0; batch < _options.MaximumBatchesPerSweep; batch++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ids = await dbContext.ApiRequestLogs
                .IgnoreQueryFilters()
                .Where(entry => entry.DataHoraFim < cutoff)
                .OrderBy(entry => entry.Id)
                .Select(entry => entry.Id)
                .Take(_options.BatchSize)
                .ToArrayAsync(cancellationToken);
            if (ids.Length == 0)
            {
                break;
            }

            total += await dbContext.ApiRequestLogs
                .IgnoreQueryFilters()
                .Where(entry => ids.Contains(entry.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        return total;
    }

    private async Task<int> DeleteBusinessAuditsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var total = 0;
        for (var batch = 0; batch < _options.MaximumBatchesPerSweep; batch++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ids = await dbContext.AuditLogs
                .IgnoreQueryFilters()
                .Where(entry => entry.DataHora < cutoff)
                .OrderBy(entry => entry.Id)
                .Select(entry => entry.Id)
                .Take(_options.BatchSize)
                .ToArrayAsync(cancellationToken);
            if (ids.Length == 0)
            {
                break;
            }

            total += await dbContext.AuditLogs
                .IgnoreQueryFilters()
                .Where(entry => ids.Contains(entry.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        return total;
    }
}
