using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Infrastructure.Organizations;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

/// <summary>
/// Corridas administrativas entre operações que, isoladamente, preservam
/// invariantes de setor, mas que sob concorrência poderiam produzir estados
/// impossíveis. Executadas contra SQL Server real, com DbContexts distintos e um
/// <see cref="Barrier"/> para garantir que as duas operações disputem de fato o
/// mesmo recurso lógico (e não acabem serializadas por acaso).
/// </summary>
[Collection(RealInfrastructureCollection.Name)]
public sealed class SectorInvariantConcurrencyTests(RealInfrastructureFixture fixture)
{
    private enum Outcome
    {
        Succeeded,
        DomainRejected,
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task ReativarSetor_E_InativarCategoria_Concorrentes_NuncaProduzemSetorAtivoComCategoriaInativa()
    {
        // Categoria dedicada com um único setor, que começa inativo.
        var category = await SeedDedicatedCategoryAsync();
        var sectorGuid = await SeedSectorAsync(category.Guid, "Pronto-Socorro concorrente", "PSC");
        await using (var db = fixture.CreateDbContext())
        {
            await fixture.CreateOrganizationCatalogService(db)
                .ChangeSectorStatusAsync(sectorGuid, false, Context(), CancellationToken.None);
        }

        using var gate = new Barrier(2);

        var reactivate = Task.Run(() => RunAsync(gate, svc =>
            svc.ChangeSectorStatusAsync(sectorGuid, true, Context(), CancellationToken.None)));
        var inactivateCategory = Task.Run(() => RunAsync(gate, svc =>
            svc.ChangeSectorCategoryStatusAsync(category.Guid, false, Context(), CancellationToken.None)));

        var (reactivateOutcome, inactivateOutcome) = (await reactivate, await inactivateCategory);

        await using var verification = fixture.CreateDbContext();
        var state = await verification.Setores.AsNoTracking()
            .Where(x => x.Guid == sectorGuid)
            .Select(x => new { SectorActive = x.Ativo, CategoryActive = x.CategoriaSetor.Ativo })
            .SingleAsync();

        // Invariável central: nunca setor ativo com categoria inativa.
        Assert.False(
            state.SectorActive && !state.CategoryActive,
            "Estado inválido: setor ativo com categoria inativa.");

        // Exatamente um lado venceu; o outro foi recusado de forma controlada (422),
        // ou ambos observaram o outro já commitado e recusaram.
        Assert.True(
            (reactivateOutcome == Outcome.Succeeded) ^ (inactivateOutcome == Outcome.Succeeded)
            || (reactivateOutcome == Outcome.DomainRejected && inactivateOutcome == Outcome.DomainRejected),
            $"Desfecho inconsistente: reativar={reactivateOutcome}, inativarCategoria={inactivateOutcome}");

        if (state.SectorActive)
        {
            Assert.True(state.CategoryActive);
            Assert.Equal(Outcome.DomainRejected, inactivateOutcome);
        }
        else if (!state.CategoryActive)
        {
            Assert.Equal(Outcome.DomainRejected, reactivateOutcome);
        }

        await AssertOutboxScopedAsync("SetorReativado", sectorGuid, reactivateOutcome == Outcome.Succeeded);
        await AssertOutboxScopedAsync(
            "CategoriaSetorInativada", category.Guid, inactivateOutcome == Outcome.Succeeded);
        if (reactivateOutcome != Outcome.Succeeded)
        {
            await AssertNoReactivationAuditAsync(sectorGuid);
        }
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task DesabilitarCompartilhamento_E_AdicionarUnidadeAtendida_Concorrentes_NuncaDeixamVinculoAtivoSemCompartilhamento()
    {
        var category = await SeedDedicatedCategoryAsync();
        var sectorGuid = await SeedSectorAsync(
            category.Guid, "CAP concorrente", "CAPC", allowsSharedActing: true);

        SectorResponse baseline;
        await using (var db = fixture.CreateDbContext())
        {
            baseline = (await fixture.CreateOrganizationCatalogService(db)
                .GetSectorAsync(fixture.ActorUserGuid, sectorGuid, CancellationToken.None))!;
        }

        var outboxAddedBefore = await CountOutboxAsync("SetorUnidadeAtendidaAdicionada");

        using var gate = new Barrier(2);

        var disable = Task.Run(() => RunAsync(gate, svc =>
            svc.UpdateSectorAsync(
                sectorGuid,
                UpdateFrom(baseline) with { AllowsSharedActing = false },
                Context(),
                CancellationToken.None)));
        var addServedUnit = Task.Run(() => RunAsync(gate, svc =>
            svc.AddSectorServedUnitAsync(
                sectorGuid,
                new AddSectorServedUnitRequest(fixture.ActingUnitGuid, new DateOnly(2026, 7, 1)),
                Context(),
                CancellationToken.None)));

        var (disableOutcome, addOutcome) = (await disable, await addServedUnit);

        await using var verification = fixture.CreateDbContext();
        var sector = await verification.Setores.AsNoTracking()
            .Where(x => x.Guid == sectorGuid)
            .Select(x => new { x.Id, x.PermiteAtuacaoCompartilhada })
            .SingleAsync();
        var servedUnits = await verification.SetoresUnidadesAtendidas.AsNoTracking()
            .Where(x => x.SetorId == sector.Id)
            .Select(x => new { x.Guid, x.Ativo })
            .ToListAsync();
        var activeServedUnits = servedUnits.Count(x => x.Ativo);

        // Invariável central: compartilhamento desabilitado ⇒ nenhum vínculo ativo.
        Assert.False(
            !sector.PermiteAtuacaoCompartilhada && activeServedUnits > 0,
            "Estado inválido: unidade atendida ativa com atuação compartilhada desabilitada.");

        if (!sector.PermiteAtuacaoCompartilhada)
        {
            // Add perdeu a corrida: recusado com 422 e nenhum vínculo criado.
            Assert.Empty(servedUnits);
            Assert.Equal(Outcome.DomainRejected, addOutcome);
        }
        else
        {
            Assert.Equal(1, activeServedUnits);
            Assert.Equal(Outcome.DomainRejected, disableOutcome);
            // Add venceu: o evento de Outbox referencia o vínculo criado.
            await AssertOutboxScopedAsync(
                "SetorUnidadeAtendidaAdicionada", servedUnits.Single().Guid, shouldExist: true);
        }

        // Auditoria/Outbox transacionais: exatamente um evento de inclusão quando o
        // add vence; nenhum quando perde (o throw ocorre antes de AddAuditAndOutbox).
        var outboxAddedAfter = await CountOutboxAsync("SetorUnidadeAtendidaAdicionada");
        Assert.Equal(
            addOutcome == Outcome.Succeeded ? 1 : 0,
            outboxAddedAfter - outboxAddedBefore);
    }

    private async Task<Outcome> RunAsync(Barrier gate, Func<OrganizationCatalogService, Task> operation)
    {
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateOrganizationCatalogService(db);
        gate.SignalAndWait();
        try
        {
            await operation(service);
            return Outcome.Succeeded;
        }
        // Perder a corrida contra a operação irmã é o único desfecho de falha
        // esperado (HTTP 422). Qualquer outra exceção sobe e reprova o teste.
        catch (DomainException)
        {
            return Outcome.DomainRejected;
        }
    }

    private async Task<SectorCategoryResponse> SeedDedicatedCategoryAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await fixture.CreateOrganizationCatalogService(db).CreateSectorCategoryAsync(
            new CreateSectorCategoryRequest($"Categoria corrida {Guid.NewGuid():N}", null),
            Context(),
            CancellationToken.None);
    }

    private async Task<Guid> SeedSectorAsync(
        Guid categoryGuid, string name, string sigla, bool allowsSharedActing = false)
    {
        await using var db = fixture.CreateDbContext();
        var request = new CreateSectorRequest(
            fixture.HiringUnitGuid,
            categoryGuid,
            $"{name} {fixture.IsolationKey}",
            sigla,
            Description: null,
            InternalLocation: null,
            Extension: null,
            Email: null,
            ResponsibleEmployeeGuid: null,
            CareRelated: false,
            AllowsScheduleAllocation: false,
            AllowsSharedActing: allowsSharedActing,
            ServedUnits: null);
        var created = await fixture.CreateOrganizationCatalogService(db).CreateSectorAsync(
            request, Context(), CancellationToken.None);
        return created.Guid;
    }

    private static UpdateSectorRequest UpdateFrom(SectorResponse s) =>
        new(
            s.Category.Guid,
            s.Name,
            s.Sigla,
            s.Description,
            s.InternalLocation,
            s.Extension,
            s.Email,
            s.Responsible?.Guid,
            s.CareRelated,
            s.AllowsScheduleAllocation,
            s.AllowsSharedActing);

    private SectorOperationContext Context() =>
        new(fixture.ActorUserGuid, Guid.NewGuid(), Guid.NewGuid().ToString("N"), "127.0.0.1");

    private async Task AssertOutboxScopedAsync(string eventType, Guid scopeGuid, bool shouldExist)
    {
        var needle = scopeGuid.ToString();
        await using var db = fixture.CreateDbContext();
        var exists = await db.OutboxMessages.AsNoTracking()
            .AnyAsync(x => x.EventType == eventType && x.Payload.Contains(needle));
        Assert.Equal(shouldExist, exists);
    }

    private async Task<int> CountOutboxAsync(string eventType)
    {
        await using var db = fixture.CreateDbContext();
        return await db.OutboxMessages.AsNoTracking().CountAsync(x => x.EventType == eventType);
    }

    private async Task AssertNoReactivationAuditAsync(Guid sectorGuid)
    {
        await using var db = fixture.CreateDbContext();
        var hasAudit = await db.AuditLogs.AsNoTracking()
            .AnyAsync(x => x.Entidade == "Setor" && x.EntidadeGuid == sectorGuid && x.Acao == "REATIVADO");
        Assert.False(hasAudit, "Reativação recusada não deve gerar auditoria REATIVADO.");
    }
}
