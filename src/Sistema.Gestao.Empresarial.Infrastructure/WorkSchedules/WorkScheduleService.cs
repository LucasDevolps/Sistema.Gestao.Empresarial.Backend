using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sistema.Gestao.Empresarial.Application.WorkSchedules;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Escalas;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.WorkSchedules;

public sealed class WorkScheduleService(AppDbContext dbContext, TimeProvider timeProvider)
    : IWorkScheduleService
{
    private const string Producer = "Sistema.Gestao.Empresarial.Api";
    private const string DuplicateNameMessage = "Já existe uma jornada de trabalho cadastrada com este nome.";

    // Nome do índice único (identificador estável do schema) usado para reconhecer a
    // violação sob concorrência; nunca é exposto ao cliente.
    private const string UniqueNameIndex = "IX_JornadasTrabalho_Nome";

    public async Task<WorkSchedulePageResponse> ListAsync(
        WorkScheduleListQuery query,
        CancellationToken cancellationToken)
    {
        // Registros excluídos logicamente já ficam de fora pelo filtro global.
        var schedules = dbContext.JornadasTrabalho.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            schedules = schedules.Where(x => x.Nome.Contains(search));
        }

        if (query.Active.HasValue)
        {
            schedules = schedules.Where(x => x.Ativo == query.Active.Value);
        }

        var total = await schedules.CountAsync(cancellationToken);
        var items = await schedules
            .OrderBy(x => x.Nome)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new WorkScheduleResponse(
                x.Guid, x.Nome, x.DiasConsecutivosTrabalho, x.DiasDescansoCiclo,
                x.MaximoDiasConsecutivos, x.Descricao, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .ToListAsync(cancellationToken);
        return new WorkSchedulePageResponse(items, query.Page, query.PageSize, total);
    }

    public Task<WorkScheduleResponse?> GetAsync(Guid workScheduleGuid, CancellationToken cancellationToken) =>
        dbContext.JornadasTrabalho.AsNoTracking()
            .Where(x => x.Guid == workScheduleGuid)
            .Select(x => new WorkScheduleResponse(
                x.Guid, x.Nome, x.DiasConsecutivosTrabalho, x.DiasDescansoCiclo,
                x.MaximoDiasConsecutivos, x.Descricao, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<WorkScheduleResponse> CreateAsync(
        CreateWorkScheduleRequest request,
        WorkScheduleOperationContext context,
        CancellationToken cancellationToken)
    {
        var guid = await ExecuteMutationAsync(async () =>
        {
            await EnsureNameIsAvailableAsync(request.Name, null, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var schedule = new JornadaTrabalho(
                Guid.NewGuid(), request.Name, request.ConsecutiveWorkDays, request.RestDays,
                request.MaximumConsecutiveWorkDays, request.Description, now);
            dbContext.JornadasTrabalho.Add(schedule);
            AddAuditAndOutbox("JornadaTrabalhoCriada", "CRIADO", schedule.Guid, context, null, Snapshot(schedule), now);
            return schedule.Guid;
        }, cancellationToken);

        return await GetAsync(guid, cancellationToken)
            ?? throw new InvalidOperationException("A jornada de trabalho persistida não pôde ser recuperada.");
    }

    public async Task<WorkScheduleResponse?> UpdateAsync(
        Guid workScheduleGuid,
        UpdateWorkScheduleRequest request,
        WorkScheduleOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var schedule = await dbContext.JornadasTrabalho
                .SingleOrDefaultAsync(x => x.Guid == workScheduleGuid, cancellationToken);
            if (schedule is null)
            {
                return false;
            }

            await EnsureNameIsAvailableAsync(request.Name, schedule.Id, cancellationToken);
            var before = Snapshot(schedule);
            var now = timeProvider.GetUtcNow();
            if (schedule.Atualizar(
                    request.Name, request.ConsecutiveWorkDays, request.RestDays,
                    request.MaximumConsecutiveWorkDays, request.Description, now))
            {
                AddAuditAndOutbox(
                    "JornadaTrabalhoAtualizada", "ATUALIZADO", schedule.Guid, context, before, Snapshot(schedule), now);
            }

            return true;
        }, cancellationToken);

        return found ? await GetAsync(workScheduleGuid, cancellationToken) : null;
    }

    public async Task<WorkScheduleResponse?> ChangeStatusAsync(
        Guid workScheduleGuid,
        bool active,
        WorkScheduleOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var schedule = await dbContext.JornadasTrabalho
                .SingleOrDefaultAsync(x => x.Guid == workScheduleGuid, cancellationToken);
            if (schedule is null)
            {
                return false;
            }

            if (schedule.Ativo == active)
            {
                return true;
            }

            var before = Snapshot(schedule);
            var now = timeProvider.GetUtcNow();
            if (active)
            {
                schedule.Reativar(now);
            }
            else
            {
                schedule.Inativar(now);
            }

            AddAuditAndOutbox(
                active ? "JornadaTrabalhoReativada" : "JornadaTrabalhoInativada",
                active ? "REATIVADO" : "INATIVADO",
                schedule.Guid,
                context,
                before,
                Snapshot(schedule),
                now);
            return true;
        }, cancellationToken);

        return found ? await GetAsync(workScheduleGuid, cancellationToken) : null;
    }

    private async Task EnsureNameIsAvailableAsync(string name, long? ignoredId, CancellationToken cancellationToken)
    {
        // Trim centralizado; a comparação insensível a caixa vem da collation da coluna Nome.
        var normalized = ChaveNegocio.Normalizar(name);
        if (await dbContext.JornadasTrabalho.AnyAsync(
                x => x.Nome == normalized && (!ignoredId.HasValue || x.Id != ignoredId.Value),
                cancellationToken))
        {
            // Mensagem fixa: não repete o valor informado nem detalhes de infraestrutura.
            throw new DuplicateBusinessKeyException(DuplicateNameMessage, field: "name");
        }
    }

    private void AddAuditAndOutbox(
        string eventType,
        string action,
        Guid entityGuid,
        WorkScheduleOperationContext context,
        object? before,
        object after,
        DateTimeOffset now)
    {
        var previousJson = before is null ? null : JsonSerializer.Serialize(before);
        var newJson = JsonSerializer.Serialize(after);
        dbContext.AuditLogs.Add(new AuditLog(
            Guid.NewGuid(), "JornadaTrabalho", entityGuid, action, context.ActorUserGuid,
            now, context.CorrelationId, context.TraceId, context.IpAddress, previousJson, newJson));

        var eventId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var envelope = new
        {
            eventId,
            messageId,
            eventType,
            eventVersion = 1,
            correlationId = context.CorrelationId,
            traceId = context.TraceId,
            occurredAt = now,
            producer = Producer,
            data = after
        };
        dbContext.OutboxMessages.Add(new OutboxMessage(
            Guid.NewGuid(), messageId, eventId, eventType, 1,
            JsonSerializer.Serialize(envelope), context.CorrelationId, context.TraceId, Producer, now));
    }

    private async Task<T> ExecuteMutationAsync<T>(Func<Task<T>> mutation, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Interlocked.Increment(ref attempt) > 1)
                {
                    dbContext.ChangeTracker.Clear();
                }

                await using var transaction = await BeginTransactionAsync(cancellationToken);
                var result = await mutation();
                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return result;
            });
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException)
        {
            // Sob concorrência duas requisições podem passar a pré-checagem e colidir no
            // índice único: devolve o mesmo 409 da pré-checagem, sem texto do SQL Server.
            throw sqlException.Errors.Cast<SqlError>()
                .Any(error => error.Message.Contains(UniqueNameIndex, StringComparison.Ordinal))
                ? new DuplicateBusinessKeyException(DuplicateNameMessage, "name", exception, sqlException.Number)
                : new DuplicateBusinessKeyException(
                    "Já existe um registro com esta chave de negócio.", null, exception, sqlException.Number);
        }
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

    private static object Snapshot(JornadaTrabalho schedule) => new
    {
        schedule.Guid,
        schedule.Nome,
        schedule.DiasConsecutivosTrabalho,
        schedule.DiasDescansoCiclo,
        schedule.MaximoDiasConsecutivos,
        schedule.Descricao,
        schedule.Ativo
    };
}
