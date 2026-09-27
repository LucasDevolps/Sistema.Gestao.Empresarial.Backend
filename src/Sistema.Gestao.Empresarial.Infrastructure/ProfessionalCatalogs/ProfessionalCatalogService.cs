using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.ProfessionalCatalogs;

public sealed class ProfessionalCatalogService(AppDbContext dbContext, TimeProvider timeProvider)
    : IProfessionalCatalogService
{
    private const string Producer = "Sistema.Gestao.Empresarial.Api";

    public async Task<ProfessionalCatalogPageResponse<ProfessionResponse>> ListProfessionsAsync(
        ProfessionalCatalogListQuery query,
        CancellationToken cancellationToken)
    {
        var professions = dbContext.Profissoes.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            professions = professions.Where(x => x.Nome.Contains(search));
        }

        if (query.Active.HasValue)
        {
            professions = professions.Where(x => x.Ativo == query.Active.Value);
        }

        var total = await professions.CountAsync(cancellationToken);
        var items = await professions
            .OrderBy(x => x.Nome)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new ProfessionResponse(
                x.Guid, x.Nome, x.Descricao, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .ToListAsync(cancellationToken);
        return new ProfessionalCatalogPageResponse<ProfessionResponse>(items, query.Page, query.PageSize, total);
    }

    public Task<ProfessionResponse?> GetProfessionAsync(Guid professionGuid, CancellationToken cancellationToken) =>
        dbContext.Profissoes.AsNoTracking()
            .Where(x => x.Guid == professionGuid)
            .Select(x => new ProfessionResponse(
                x.Guid, x.Nome, x.Descricao, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ProfessionResponse> CreateProfessionAsync(
        CreateProfessionalCatalogRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken)
    {
        var professionGuid = await ExecuteMutationAsync(async () =>
        {
            await EnsureProfessionNameIsAvailableAsync(request.Name, null, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var profession = new Profissao(Guid.NewGuid(), request.Name, request.Description, now);
            dbContext.Profissoes.Add(profession);
            AddAuditAndOutbox(
                "ProfissaoCriada", "Profissao", "CRIADA", profession.Guid,
                context, null, Snapshot(profession), now);
            return profession.Guid;
        }, cancellationToken);

        return await GetProfessionAsync(professionGuid, cancellationToken)
            ?? throw new InvalidOperationException("A profissão persistida não pôde ser recuperada.");
    }

    public async Task<ProfessionResponse?> UpdateProfessionAsync(
        Guid professionGuid,
        UpdateProfessionalCatalogRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var profession = await dbContext.Profissoes
                .SingleOrDefaultAsync(x => x.Guid == professionGuid, cancellationToken);
            if (profession is null)
            {
                return false;
            }

            await EnsureProfessionNameIsAvailableAsync(request.Name, profession.Id, cancellationToken);
            var before = Snapshot(profession);
            var now = timeProvider.GetUtcNow();
            if (profession.Atualizar(request.Name, request.Description, now))
            {
                AddAuditAndOutbox(
                    "ProfissaoAtualizada", "Profissao", "ATUALIZADA", profession.Guid,
                    context, before, Snapshot(profession), now);
            }
            return true;
        }, cancellationToken);

        return found ? await GetProfessionAsync(professionGuid, cancellationToken) : null;
    }

    public async Task<ProfessionResponse?> ChangeProfessionStatusAsync(
        Guid professionGuid,
        bool active,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var profession = await dbContext.Profissoes
                .SingleOrDefaultAsync(x => x.Guid == professionGuid, cancellationToken);
            if (profession is null)
            {
                return false;
            }

            if (profession.Ativo == active)
            {
                return true;
            }

            if (!active && await dbContext.Funcionarios.AnyAsync(
                    x => x.ProfissaoId == profession.Id && x.Ativo,
                    cancellationToken))
            {
                throw new DomainException("A profissão possui funcionários ativos e não pode ser inativada.");
            }

            var before = Snapshot(profession);
            var now = timeProvider.GetUtcNow();
            if (active)
            {
                profession.Reativar(now);
            }
            else
            {
                profession.Inativar(now);
            }

            AddAuditAndOutbox(
                active ? "ProfissaoReativada" : "ProfissaoInativada",
                "Profissao",
                active ? "REATIVADA" : "INATIVADA",
                profession.Guid,
                context,
                before,
                Snapshot(profession),
                now);
            return true;
        }, cancellationToken);

        return found ? await GetProfessionAsync(professionGuid, cancellationToken) : null;
    }

    public async Task<ProfessionalCatalogPageResponse<PositionResponse>> ListPositionsAsync(
        ProfessionalCatalogListQuery query,
        CancellationToken cancellationToken)
    {
        var positions = dbContext.Cargos.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            positions = positions.Where(x => x.Nome.Contains(search));
        }

        if (query.Active.HasValue)
        {
            positions = positions.Where(x => x.Ativo == query.Active.Value);
        }

        var total = await positions.CountAsync(cancellationToken);
        var items = await positions
            .OrderBy(x => x.Nome)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new PositionResponse(
                x.Guid, x.Nome, x.Descricao, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .ToListAsync(cancellationToken);
        return new ProfessionalCatalogPageResponse<PositionResponse>(items, query.Page, query.PageSize, total);
    }

    public Task<PositionResponse?> GetPositionAsync(Guid positionGuid, CancellationToken cancellationToken) =>
        dbContext.Cargos.AsNoTracking()
            .Where(x => x.Guid == positionGuid)
            .Select(x => new PositionResponse(
                x.Guid, x.Nome, x.Descricao, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PositionResponse> CreatePositionAsync(
        CreateProfessionalCatalogRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken)
    {
        var positionGuid = await ExecuteMutationAsync(async () =>
        {
            await EnsurePositionNameIsAvailableAsync(request.Name, null, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var position = new Cargo(Guid.NewGuid(), request.Name, request.Description, now);
            dbContext.Cargos.Add(position);
            AddAuditAndOutbox(
                "CargoCriado", "Cargo", "CRIADO", position.Guid,
                context, null, Snapshot(position), now);
            return position.Guid;
        }, cancellationToken);

        return await GetPositionAsync(positionGuid, cancellationToken)
            ?? throw new InvalidOperationException("O cargo persistido não pôde ser recuperado.");
    }

    public async Task<PositionResponse?> UpdatePositionAsync(
        Guid positionGuid,
        UpdateProfessionalCatalogRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var position = await dbContext.Cargos
                .SingleOrDefaultAsync(x => x.Guid == positionGuid, cancellationToken);
            if (position is null)
            {
                return false;
            }

            await EnsurePositionNameIsAvailableAsync(request.Name, position.Id, cancellationToken);
            var before = Snapshot(position);
            var now = timeProvider.GetUtcNow();
            if (position.Atualizar(request.Name, request.Description, now))
            {
                AddAuditAndOutbox(
                    "CargoAtualizado", "Cargo", "ATUALIZADO", position.Guid,
                    context, before, Snapshot(position), now);
            }
            return true;
        }, cancellationToken);

        return found ? await GetPositionAsync(positionGuid, cancellationToken) : null;
    }

    public async Task<PositionResponse?> ChangePositionStatusAsync(
        Guid positionGuid,
        bool active,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var position = await dbContext.Cargos
                .SingleOrDefaultAsync(x => x.Guid == positionGuid, cancellationToken);
            if (position is null)
            {
                return false;
            }

            if (position.Ativo == active)
            {
                return true;
            }

            if (!active && await dbContext.Funcionarios.AnyAsync(
                    x => x.CargoId == position.Id && x.Ativo,
                    cancellationToken))
            {
                throw new DomainException("O cargo possui funcionários ativos e não pode ser inativado.");
            }

            var before = Snapshot(position);
            var now = timeProvider.GetUtcNow();
            if (active)
            {
                position.Reativar(now);
            }
            else
            {
                position.Inativar(now);
            }

            AddAuditAndOutbox(
                active ? "CargoReativado" : "CargoInativado",
                "Cargo",
                active ? "REATIVADO" : "INATIVADO",
                position.Guid,
                context,
                before,
                Snapshot(position),
                now);
            return true;
        }, cancellationToken);

        return found ? await GetPositionAsync(positionGuid, cancellationToken) : null;
    }

    public async Task<ProfessionalCatalogPageResponse<ProfessionalLevelResponse>> ListLevelsAsync(
        ProfessionalCatalogListQuery query,
        CancellationToken cancellationToken)
    {
        // Registros excluídos logicamente já ficam de fora pelo filtro global.
        var levels = dbContext.NiveisProfissionais.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            levels = levels.Where(x => x.Codigo.Contains(search) || x.Nome.Contains(search));
        }

        if (query.Active.HasValue)
        {
            levels = levels.Where(x => x.Ativo == query.Active.Value);
        }

        var total = await levels.CountAsync(cancellationToken);
        // A ordem pode se repetir; o nome (único entre registros não excluídos)
        // torna a ordenação determinística.
        var items = await levels
            .OrderBy(x => x.Ordem)
            .ThenBy(x => x.Nome)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new ProfessionalLevelResponse(
                x.Guid, x.Codigo, x.Nome, x.Ordem, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .ToListAsync(cancellationToken);
        return new ProfessionalCatalogPageResponse<ProfessionalLevelResponse>(
            items, query.Page, query.PageSize, total);
    }

    public Task<ProfessionalLevelResponse?> GetLevelAsync(Guid levelGuid, CancellationToken cancellationToken) =>
        dbContext.NiveisProfissionais.AsNoTracking()
            .Where(x => x.Guid == levelGuid)
            .Select(x => new ProfessionalLevelResponse(
                x.Guid, x.Codigo, x.Nome, x.Ordem, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ProfessionalLevelResponse> CreateLevelAsync(
        CreateProfessionalLevelRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken)
    {
        var levelGuid = await ExecuteMutationAsync(async () =>
        {
            await EnsureLevelKeysAreAvailableAsync(request.Code, request.Name, null, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var level = new NivelProfissional(Guid.NewGuid(), request.Code, request.Name, request.Order, now);
            dbContext.NiveisProfissionais.Add(level);
            AddAuditAndOutbox(
                "NivelProfissionalCriado", "NivelProfissional", "CRIADO", level.Guid,
                context, null, Snapshot(level), now);
            return level.Guid;
        }, cancellationToken);

        return await GetLevelAsync(levelGuid, cancellationToken)
            ?? throw new InvalidOperationException("O nível profissional persistido não pôde ser recuperado.");
    }

    public async Task<ProfessionalLevelResponse?> UpdateLevelAsync(
        Guid levelGuid,
        UpdateProfessionalLevelRequest request,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var level = await dbContext.NiveisProfissionais
                .SingleOrDefaultAsync(x => x.Guid == levelGuid, cancellationToken);
            if (level is null)
            {
                return false;
            }

            await EnsureLevelKeysAreAvailableAsync(request.Code, request.Name, level.Id, cancellationToken);
            var before = Snapshot(level);
            var now = timeProvider.GetUtcNow();
            if (level.Atualizar(request.Code, request.Name, request.Order, now))
            {
                AddAuditAndOutbox(
                    "NivelProfissionalAtualizado", "NivelProfissional", "ATUALIZADO", level.Guid,
                    context, before, Snapshot(level), now);
            }
            return true;
        }, cancellationToken);

        return found ? await GetLevelAsync(levelGuid, cancellationToken) : null;
    }

    public Task<bool> DeleteLevelAsync(
        Guid levelGuid,
        ProfessionalCatalogOperationContext context,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            async () =>
            {
                var level = await dbContext.NiveisProfissionais
                    .SingleOrDefaultAsync(x => x.Guid == levelGuid, cancellationToken);
                if (level is null)
                {
                    return false;
                }

                // Funcionários ativos E inativos: o vínculo é histórico e as projeções
                // de funcionário leem o nível por navegação obrigatória — um nível
                // excluído sumiria com o funcionário das listagens.
                if (await dbContext.Funcionarios.AnyAsync(x => x.NivelId == level.Id, cancellationToken))
                {
                    throw new DomainException(
                        "O nível profissional está vinculado a funcionários e não pode ser excluído.");
                }

                var before = Snapshot(level);
                var now = timeProvider.GetUtcNow();
                level.ExcluirLogicamente(context.ActorUserGuid, now);
                AddAuditAndOutbox(
                    "NivelProfissionalExcluido", "NivelProfissional", "EXCLUIDO", level.Guid,
                    context, before, Snapshot(level), now);
                return true;
            },
            cancellationToken,
            ct => ProfessionalLevelUsageLock.AcquireForDeletionAsync(dbContext, levelGuid, ct));

    private async Task EnsureProfessionNameIsAvailableAsync(
        string name,
        long? ignoredId,
        CancellationToken cancellationToken)
    {
        // Trim centralizado; a comparação insensível a caixa vem da collation da coluna Nome.
        var normalized = ChaveNegocio.Normalizar(name);
        if (await dbContext.Profissoes.AnyAsync(
                x => x.Nome == normalized && (!ignoredId.HasValue || x.Id != ignoredId.Value),
                cancellationToken))
        {
            // Mensagem fixa: não repete o valor informado (evita PII, log forging e
            // acoplamento do front-end ao texto). O `field` usa o nome do contrato público.
            throw new DuplicateBusinessKeyException(
                "Já existe uma profissão cadastrada com este título.", field: "name");
        }
    }

    private async Task EnsurePositionNameIsAvailableAsync(
        string name,
        long? ignoredId,
        CancellationToken cancellationToken)
    {
        var normalized = ChaveNegocio.Normalizar(name);
        if (await dbContext.Cargos.AnyAsync(
                x => x.Nome == normalized && (!ignoredId.HasValue || x.Id != ignoredId.Value),
                cancellationToken))
        {
            throw new DuplicateBusinessKeyException(
                "Já existe um cargo cadastrado com este nome.", field: "name");
        }
    }

    private async Task EnsureLevelKeysAreAvailableAsync(
        string code,
        string name,
        long? ignoredId,
        CancellationToken cancellationToken)
    {
        // Mesma normalização da entidade; a insensibilidade a caixa do nome vem da
        // collation das colunas (o código já é gravado em maiúsculas).
        var normalizedCode = NivelProfissional.NormalizarCodigo(code);
        if (await dbContext.NiveisProfissionais.AnyAsync(
                x => x.Codigo == normalizedCode && (!ignoredId.HasValue || x.Id != ignoredId.Value),
                cancellationToken))
        {
            throw new DuplicateBusinessKeyException(
                "Já existe um nível profissional cadastrado com este código.", field: "code");
        }

        var normalizedName = ChaveNegocio.Normalizar(name);
        if (await dbContext.NiveisProfissionais.AnyAsync(
                x => x.Nome == normalizedName && (!ignoredId.HasValue || x.Id != ignoredId.Value),
                cancellationToken))
        {
            throw new DuplicateBusinessKeyException(
                "Já existe um nível profissional cadastrado com este nome.", field: "name");
        }
    }

    private void AddAuditAndOutbox(
        string eventType,
        string entity,
        string action,
        Guid entityGuid,
        ProfessionalCatalogOperationContext context,
        object? before,
        object after,
        DateTimeOffset now)
    {
        var previousJson = before is null ? null : JsonSerializer.Serialize(before);
        var newJson = JsonSerializer.Serialize(after);
        dbContext.AuditLogs.Add(new AuditLog(
            Guid.NewGuid(), entity, entityGuid, action, context.ActorUserGuid,
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

    private async Task<T> ExecuteMutationAsync<T>(
        Func<Task<T>> mutation,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<IAsyncDisposable>>? acquireLock = null)
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
                // Lock lógico opcional, adquirido dentro da transação e mantido até o
                // commit (applock com @LockOwner='Transaction').
                await using var mutationLock = acquireLock is null
                    ? null
                    : await acquireLock(cancellationToken);
                var result = await mutation();
                await dbContext.SaveChangesAsync(cancellationToken);
                await CommitAsync(transaction, cancellationToken);
                return result;
            });
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException)
        {
            // Sob concorrência duas requisições podem passar a pré-checagem e colidir
            // no índice único. A tradução identifica qual chave de negócio colidiu para
            // devolver o `field` correto, sem propagar o texto localizado do SQL Server
            // nem o nome do índice. Só o número do erro segue como metadado de log.
            throw TranslateUniqueViolation(sqlException, exception);
        }
    }

    /// <summary>
    /// Índices únicos deste serviço cuja violação (SQL Server 2601/2627) corresponde a
    /// uma chave de negócio pública. O nome do índice é um identificador estável do
    /// schema (não varia com o idioma do servidor) e nunca é exposto ao cliente.
    /// </summary>
    private static readonly (string IndexName, string Field, string Message)[] UniqueBusinessKeys =
    [
        ("IX_Profissoes_Nome", "name", "Já existe uma profissão cadastrada com este título."),
        ("IX_Cargos_Nome", "name", "Já existe um cargo cadastrado com este nome."),
        ("IX_NiveisProfissionais_Codigo", "code", "Já existe um nível profissional cadastrado com este código."),
        ("IX_NiveisProfissionais_Nome", "name", "Já existe um nível profissional cadastrado com este nome."),
    ];

    private static DuplicateBusinessKeyException TranslateUniqueViolation(
        SqlException sqlException,
        DbUpdateException source)
    {
        foreach (SqlError error in sqlException.Errors)
        {
            foreach (var (indexName, field, message) in UniqueBusinessKeys)
            {
                if (error.Message.Contains(indexName, StringComparison.Ordinal))
                {
                    return new DuplicateBusinessKeyException(message, field, source, sqlException.Number);
                }
            }
        }

        // Sem classificação segura: conflito genérico, sem `field`.
        return new DuplicateBusinessKeyException(
            "Já existe um registro com esta chave de negócio.",
            field: null,
            innerException: source,
            sqlErrorNumber: sqlException.Number);
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    private static object Snapshot(Profissao profession) => new
    {
        profession.Guid,
        profession.Nome,
        profession.Descricao,
        profession.Ativo
    };

    private static object Snapshot(Cargo position) => new
    {
        position.Guid,
        position.Nome,
        position.Descricao,
        position.Ativo
    };

    private static object Snapshot(NivelProfissional level) => new
    {
        level.Guid,
        level.Codigo,
        level.Nome,
        level.Ordem,
        level.Ativo,
        level.Excluido,
        level.ExcluidoEm,
        level.ExcluidoPor
    };
}
