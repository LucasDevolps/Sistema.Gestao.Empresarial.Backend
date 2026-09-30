using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.WorkSchedules;
using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

/// <summary>
/// Jornadas de trabalho (issue #51) contra SQL Server real: migration, índice único
/// filtrado com collation <c>Latin1_General_CI_AS</c>, check constraints, concorrência
/// da chave de negócio e transacionalidade Audit + Outbox.
/// </summary>
[Collection(RealInfrastructureCollection.Name)]
public sealed class WorkScheduleDatabaseTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task Schema_DeveTerIndiceUnicoFiltradoCollationEChecks()
    {
        await using var db = fixture.CreateDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_JornadasTrabalho_Nome' AND is_unique = 1 AND filter_definition LIKE '%Excluido%'),
                (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('sge.JornadasTrabalho') AND name = 'Nome' AND collation_name = 'Latin1_General_CI_AS'),
                (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('sge.JornadasTrabalho')),
                (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('sge.JornadasTrabalho') AND name = 'Versao' AND system_type_id = 189),
                (SELECT COUNT(*) FROM sys.foreign_keys WHERE referenced_object_id = OBJECT_ID('sge.JornadasTrabalho') AND delete_referential_action <> 0),
                (SELECT COUNT(*) FROM [sge].[JornadasTrabalho] WHERE [Excluido] = 0 AND [Nome] IN ('6x1', '5x2')),
                (SELECT COUNT(*) FROM [sge].[Permissoes] WHERE [Codigo] IN ('JORNADA_TRABALHO_VISUALIZAR', 'JORNADA_TRABALHO_CRIAR', 'JORNADA_TRABALHO_EDITAR') AND [Ativo] = 1);
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(1, reader.GetInt32(1));
        Assert.Equal(3, reader.GetInt32(2));
        Assert.Equal(1, reader.GetInt32(3));
        Assert.Equal(0, reader.GetInt32(4));
        // Nenhuma jornada (6x1/5x2) é semeada pela migration.
        Assert.Equal(0, reader.GetInt32(5));
        Assert.Equal(3, reader.GetInt32(6));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task BancoDeveRejeitarParametrosInvalidosMesmoSemPassarPeloDominio()
    {
        (int Work, int RestDays, int Max)[] invalid = [(0, 1, 6), (6, 0, 6), (6, 1, 0), (6, 1, 7), (-1, 1, 6)];
        foreach (var (work, rest, max) in invalid)
        {
            await using var db = fixture.CreateDbContext();
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [sge].[JornadasTrabalho]
                ([Guid], [Nome], [DiasConsecutivosTrabalho], [DiasDescansoCiclo], [MaximoDiasConsecutivos],
                 [Ativo], [Excluido], [DataCriacao], [DataAtualizacao])
            VALUES (NEWID(), {0}, {1}, {2}, {3}, 1, 0, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())
            """,
            $"CK {fixture.IsolationKey} {Guid.NewGuid():N}", work, rest, max));
            Assert.Equal(547, error.Number);
        }
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task NomesConcorrentesComVariacaoDeCaixaEEspacos_DevemPersistirSomenteUmaJornada()
    {
        var name = $"6x1 concorrente {fixture.IsolationKey}";

        var attempts = await Task.WhenAll(
            TryCreateAsync(name),
            TryCreateAsync(name.ToUpperInvariant()),
            TryCreateAsync($"  {name.ToLowerInvariant()}  "),
            TryCreateAsync(name));

        Assert.Single(attempts, x => x.Response is not null);
        Assert.All(
            attempts.Where(x => x.Response is null),
            x => Assert.Equal("name", Assert.IsType<DuplicateBusinessKeyException>(x.Error).Field));
        await using var verification = fixture.CreateDbContext();
        Assert.Equal(1, await verification.JornadasTrabalho.CountAsync(x => x.Nome == name));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task NomeComVariacaoDeCaixa_DeveSerRejeitadoNoCadastroENaEdicao()
    {
        var name = $"Adm {fixture.IsolationKey}";
        var first = await TryCreateAsync(name);
        var other = await TryCreateAsync($"Outra {fixture.IsolationKey}");

        var duplicateOnCreate = await TryCreateAsync(name.ToUpperInvariant());
        await using var db = fixture.CreateDbContext();
        var duplicateOnUpdate = await Record.ExceptionAsync(() => fixture.CreateWorkScheduleService(db).UpdateAsync(
            other.Response!.Guid,
            new UpdateWorkScheduleRequest(name.ToLowerInvariant(), 6, 1, 6, null),
            Context(),
            CancellationToken.None));

        Assert.NotNull(first.Response);
        Assert.Equal("name", Assert.IsType<DuplicateBusinessKeyException>(duplicateOnCreate.Error).Field);
        Assert.Equal("name", Assert.IsType<DuplicateBusinessKeyException>(duplicateOnUpdate).Field);
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task CicloCompleto_DevePersistirAuditoriaEOutboxNaMesmaTransacao()
    {
        var correlation = Guid.NewGuid();
        var context = new WorkScheduleOperationContext(Guid.NewGuid(), correlation, "trace", "127.0.0.1");
        WorkScheduleResponse created;
        await using (var db = fixture.CreateDbContext())
        {
            var service = fixture.CreateWorkScheduleService(db);
            created = await service.CreateAsync(
                new CreateWorkScheduleRequest($"Ciclo {fixture.IsolationKey}", 5, 2, 5, null), context, CancellationToken.None);
            await service.UpdateAsync(
                created.Guid,
                new UpdateWorkScheduleRequest($"Ciclo editado {fixture.IsolationKey}", 6, 1, 6, "d"),
                context,
                CancellationToken.None);
            await service.ChangeStatusAsync(created.Guid, false, context, CancellationToken.None);
            var inactive = await service.GetAsync(created.Guid, CancellationToken.None);
            Assert.False(inactive!.Active);
            await service.ChangeStatusAsync(created.Guid, true, context, CancellationToken.None);
        }

        await using var verification = fixture.CreateDbContext();
        var actions = await verification.AuditLogs
            .Where(x => x.CorrelationId == correlation)
            .OrderBy(x => x.Id)
            .Select(x => x.Acao)
            .ToListAsync();
        var events = await verification.OutboxMessages
            .Where(x => x.CorrelationId == correlation)
            .OrderBy(x => x.Id)
            .Select(x => x.EventType)
            .ToListAsync();
        Assert.Equal(["CRIADO", "ATUALIZADO", "INATIVADO", "REATIVADO"], actions);
        Assert.Equal(
            ["JornadaTrabalhoCriada", "JornadaTrabalhoAtualizada", "JornadaTrabalhoInativada", "JornadaTrabalhoReativada"],
            events);
        var stored = await verification.JornadasTrabalho.AsNoTracking().SingleAsync(x => x.Guid == created.Guid);
        Assert.True(stored.Ativo);
        Assert.False(stored.Excluido);
        Assert.NotEmpty(stored.Versao);
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task FalhaDeDominio_NaoDevePersistirJornadaAuditoriaNemOutbox()
    {
        var correlation = Guid.NewGuid();
        var name = $"Invalida {fixture.IsolationKey}";
        await using (var db = fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<DomainException>(() => fixture.CreateWorkScheduleService(db).CreateAsync(
                new CreateWorkScheduleRequest(name, 6, 1, 7, null),
                new WorkScheduleOperationContext(Guid.NewGuid(), correlation, "trace", null),
                CancellationToken.None));
        }

        await using var verification = fixture.CreateDbContext();
        Assert.False(await verification.JornadasTrabalho.AnyAsync(x => x.Nome == name));
        Assert.False(await verification.AuditLogs.AnyAsync(x => x.CorrelationId == correlation));
        Assert.False(await verification.OutboxMessages.AnyAsync(x => x.CorrelationId == correlation));
    }

    private async Task<Attempt> TryCreateAsync(string name)
    {
        try
        {
            await using var db = fixture.CreateDbContext();
            var response = await fixture.CreateWorkScheduleService(db).CreateAsync(
                new CreateWorkScheduleRequest(name, 6, 1, 6, null), Context(), CancellationToken.None);
            return new Attempt(response, null);
        }
        catch (Exception exception)
        {
            return new Attempt(null, exception);
        }
    }

    private static WorkScheduleOperationContext Context() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("N"), "127.0.0.1");

    private sealed record Attempt(WorkScheduleResponse? Response, Exception? Error);
}
