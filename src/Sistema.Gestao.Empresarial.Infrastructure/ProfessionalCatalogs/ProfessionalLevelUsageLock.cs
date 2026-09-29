using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.ProfessionalCatalogs;

/// <summary>
/// Lock lógico por nível profissional que protege a invariável
/// "nível excluído logicamente ⇒ nenhum funcionário vinculado a ele".
/// </summary>
/// <remarks>
/// Sem este lock, a exclusão de um nível e o cadastro/edição de um funcionário que
/// o seleciona podem passar as duas pré-checagens em paralelo e terminar com um
/// funcionário apontando para um nível excluído — que o filtro global de exclusão
/// lógica esconderia das listagens. A exclusão adquire o recurso em modo
/// <c>Exclusive</c>; o vínculo de funcionário, em modo <c>Shared</c> (cadastros de
/// funcionários com o mesmo nível não se bloqueiam entre si). Mesmo padrão de
/// <c>sp_getapplock</c> dos demais serviços: <c>@LockOwner = 'Transaction'</c>, portanto
/// deve ser chamado dentro da transação da unidade de trabalho e é liberado no
/// commit/rollback. No provider InMemory (testes) um semáforo em processo serializa.
/// </remarks>
internal static class ProfessionalLevelUsageLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> InProcessLocks = new();

    public static Task<IAsyncDisposable> AcquireForDeletionAsync(
        AppDbContext dbContext,
        Guid levelGuid,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, levelGuid, "Exclusive", cancellationToken);

    public static Task<IAsyncDisposable> AcquireForAssignmentAsync(
        AppDbContext dbContext,
        Guid levelGuid,
        CancellationToken cancellationToken) =>
        AcquireAsync(dbContext, levelGuid, "Shared", cancellationToken);

    private static async Task<IAsyncDisposable> AcquireAsync(
        AppDbContext dbContext,
        Guid levelGuid,
        string lockMode,
        CancellationToken cancellationToken)
    {
        var resource = $"sge:nivel-profissional:{levelGuid.ToString("D", CultureInfo.InvariantCulture)}";
        if (!dbContext.Database.IsSqlServer())
        {
            var semaphore = InProcessLocks.GetOrAdd(resource, static _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            return new SemaphoreReleaser(semaphore);
        }

        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = @lockMode,
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            SELECT @result;
            """;
        AddParameter(command, "@resource", resource);
        AddParameter(command, "@lockMode", lockMode);
        var result = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (result < 0)
        {
            // Timeout/deadlock: erro controlado e genérico (HTTP 503 pelo
            // GlobalExceptionHandler), sem expor o recurso interno nem SQL.
            throw new TimeoutException(
                "Não foi possível serializar a operação com o nível profissional no momento. Tente novamente.");
        }

        return NoopAsyncDisposable.Instance;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed class SemaphoreReleaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoopAsyncDisposable Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
