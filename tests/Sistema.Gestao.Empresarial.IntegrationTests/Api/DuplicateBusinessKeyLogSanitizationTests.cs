using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

/// <summary>
/// Garante que o conflito de chave de negócio não vaza o valor informado pelo
/// usuário (título/nome) nem PII (e-mail) para <b>nenhum</b> sink de log ao passar
/// pelo pipeline HTTP real, sem deixar de registrar o diagnóstico correlacionável.
/// </summary>
public sealed class DuplicateBusinessKeyLogSanitizationTests
    : IClassFixture<DuplicateBusinessKeyLogSanitizationTests.LogCapturingApiFactory>
{
    private readonly LogCapturingApiFactory _factory;

    public DuplicateBusinessKeyLogSanitizationTests(LogCapturingApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Profissao_Duplicada_NaoRegistraOTituloInformadoEmNenhumLog()
    {
        using var client = _factory.CreateApiClient();
        var titulo = $"Sensível-Título-{Guid.NewGuid():N}";
        _factory.ClearLogs();

        await client.PostAsJsonAsync("/api/profissoes", new { name = titulo, description = (string?)null });
        // Variação por espaços (independe de provider/collation) — equivale à regra de negócio.
        var conflito = await client.PostAsJsonAsync(
            "/api/profissoes", new { name = $"   {titulo}   ", description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, conflito.StatusCode);
        AssertNoLogContains(titulo);
        AssertDiagnosticoDeDuplicidadeRegistrado();
    }

    [Fact]
    public async Task Funcionario_EmailDuplicado_NaoRegistraOEmailEmNenhumLog()
    {
        var seed = await _factory.SeedEmployeeGraphAsync();
        using var client = _factory.CreateApiClient();
        var email = $"sensitive-user-{Guid.NewGuid():N}@hospital.test";
        _factory.ClearLogs();

        await client.PostAsJsonAsync("/api/funcionarios", EmployeePayload(seed, email));
        var conflito = await client.PostAsJsonAsync("/api/funcionarios", EmployeePayload(seed, email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, conflito.StatusCode);
        AssertNoLogContains(email);
        AssertDiagnosticoDeDuplicidadeRegistrado();
    }

    private void AssertNoLogContains(string forbidden)
    {
        var offending = _factory.Logs
            .Where(entry =>
                entry.Message.Contains(forbidden, StringComparison.OrdinalIgnoreCase)
                || (entry.State?.Contains(forbidden, StringComparison.OrdinalIgnoreCase) ?? false)
                || (entry.Exception?.Contains(forbidden, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(entry => $"[{entry.Level}] {entry.Category}: {entry.Message}")
            .ToList();

        Assert.True(offending.Count == 0, "Valor sensível encontrado em log(s): " + string.Join(" | ", offending));
    }

    private void AssertDiagnosticoDeDuplicidadeRegistrado()
    {
        Assert.Contains(_factory.Logs, entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains(Domain.Common.DuplicateBusinessKeyException.ErrorCode, StringComparison.Ordinal)
            && entry.Message.Contains("CorrelationId=", StringComparison.Ordinal));
    }

    private static object EmployeePayload(DuplicateBusinessKeyContractApiFactory.EmployeeSeed seed, string email) => new
    {
        name = "Funcionário de Log",
        email,
        phone = (string?)null,
        professionGuid = seed.ProfessionGuid,
        positionGuid = seed.PositionGuid,
        levelGuid = seed.LevelGuid,
        hiringUnitGuid = seed.HiringUnitGuid,
        admissionDate = "2026-01-02",
        actingUnits = Array.Empty<object>(),
        sectors = Array.Empty<object>()
    };

    public sealed class LogCapturingApiFactory : DuplicateBusinessKeyContractApiFactory
    {
        private readonly CapturingLoggerProvider _provider = new();

        public IReadOnlyList<CapturedLog> Logs => _provider.Entries.ToArray();

        public void ClearLogs() => _provider.Entries.Clear();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddProvider(_provider);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _provider.Dispose();
            }
        }
    }

    public sealed record CapturedLog(LogLevel Level, string Category, string Message, string? State, string? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<CapturedLog> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                entries.Enqueue(new CapturedLog(
                    logLevel,
                    category,
                    formatter(state, exception),
                    state?.ToString(),
                    exception?.ToString()));
            }
        }
    }
}
