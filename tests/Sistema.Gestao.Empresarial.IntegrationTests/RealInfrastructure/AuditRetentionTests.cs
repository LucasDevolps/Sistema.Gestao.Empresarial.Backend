using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Infrastructure.Auditing;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed class AuditRetentionTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task Sweep_DeveRemoverSomenteRegistrosForaDaRetencao()
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var expiredHttpGuid = Guid.NewGuid();
        var retainedHttpGuid = Guid.NewGuid();
        var expiredBusinessGuid = Guid.NewGuid();
        var retainedBusinessGuid = Guid.NewGuid();
        await using (var db = fixture.CreateDbContext())
        {
            var expiredHttp = CreateHttpLog(expiredHttpGuid, now.AddDays(-181));
            var expiredBusiness = CreateBusinessAudit(expiredBusinessGuid, now.AddDays(-1826));
            expiredHttp.ExcluirLogicamente(Guid.NewGuid(), now.AddDays(-181));
            expiredBusiness.ExcluirLogicamente(Guid.NewGuid(), now.AddDays(-1826));
            db.ApiRequestLogs.AddRange(
                expiredHttp,
                CreateHttpLog(retainedHttpGuid, now.AddDays(-179)));
            db.AuditLogs.AddRange(
                expiredBusiness,
                CreateBusinessAudit(retainedBusinessGuid, now.AddDays(-1824)));
            await db.SaveChangesAsync();
        }

        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(now))
            .AddDbContext<AppDbContext>(options => options.UseSqlServer(fixture.DatabaseConnectionString))
            .BuildServiceProvider();
        await using var provider = services;
        var worker = new AuditRetentionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<TimeProvider>(),
            Options.Create(new AuditRetentionOptions
            {
                HttpAccessLogDays = 180,
                BusinessAuditDays = 1825,
                BatchSize = 100,
                MaximumBatchesPerSweep = 2
            }),
            NullLogger<AuditRetentionWorker>.Instance);

        await worker.SweepAsync(CancellationToken.None);

        await using var verification = fixture.CreateDbContext();
        Assert.False(await verification.ApiRequestLogs.IgnoreQueryFilters().AnyAsync(entry => entry.Guid == expiredHttpGuid));
        Assert.True(await verification.ApiRequestLogs.AnyAsync(entry => entry.Guid == retainedHttpGuid));
        Assert.False(await verification.AuditLogs.IgnoreQueryFilters().AnyAsync(entry => entry.Guid == expiredBusinessGuid));
        Assert.True(await verification.AuditLogs.AnyAsync(entry => entry.Guid == retainedBusinessGuid));
    }

    private static ApiRequestLog CreateHttpLog(Guid guid, DateTimeOffset timestamp) =>
        new(
            guid, timestamp, timestamp.AddSeconds(1), "GET", "/health/live",
            null, "{}", null, "{}", null, 200, 1, true, Guid.NewGuid(),
            Guid.NewGuid().ToString("N"), null, null, null, null, "Production", null);

    private static AuditLog CreateBusinessAudit(Guid guid, DateTimeOffset timestamp) =>
        new(
            guid, "RetentionTest", Guid.NewGuid(), "TESTE", null,
            timestamp, Guid.NewGuid(), Guid.NewGuid().ToString("N"), null);

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }
}
