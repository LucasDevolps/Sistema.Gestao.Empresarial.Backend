using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Organizations;

public sealed class OrganizationCatalogService(AppDbContext dbContext, TimeProvider timeProvider)
    : IOrganizationCatalogService
{
    private const string Producer = "Sistema.Gestao.Empresarial.Api";

    public async Task<OrganizationResponse> GetCurrentOrganizationAsync(
        Guid actorUserGuid,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        return await dbContext.Organizacoes.AsNoTracking()
            .Where(x => x.Id == organizationId)
            .Select(x => new OrganizationResponse(
                x.Guid, x.Nome, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .SingleAsync(cancellationToken);
    }

    public async Task<HospitalUnitPageResponse> ListHospitalUnitsAsync(
        Guid actorUserGuid,
        OrganizationCatalogListQuery query,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        var units = dbContext.UnidadesHospitalares.AsNoTracking()
            .Where(x => x.OrganizacaoId == organizationId);
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            units = units.Where(x => x.Nome.Contains(search));
        }

        if (query.Active.HasValue)
        {
            units = units.Where(x => x.Ativo == query.Active.Value);
        }

        var total = await units.CountAsync(cancellationToken);
        var items = await units
            .OrderBy(x => x.Nome)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new HospitalUnitResponse(
                x.Guid,
                x.Nome,
                x.Ativo,
                new OrganizationReferenceResponse(x.Organizacao.Guid, x.Organizacao.Nome),
                x.DataCriacao,
                x.DataAtualizacao))
            .ToListAsync(cancellationToken);

        return new HospitalUnitPageResponse(items, query.Page, query.PageSize, total);
    }

    public async Task<HospitalUnitResponse?> GetHospitalUnitAsync(
        Guid actorUserGuid,
        Guid unitGuid,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        return await dbContext.UnidadesHospitalares.AsNoTracking()
            .Where(x => x.Guid == unitGuid && x.OrganizacaoId == organizationId)
            .Select(x => new HospitalUnitResponse(
                x.Guid,
                x.Nome,
                x.Ativo,
                new OrganizationReferenceResponse(x.Organizacao.Guid, x.Organizacao.Nome),
                x.DataCriacao,
                x.DataAtualizacao))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<SectorPageResponse> ListSectorsAsync(
        Guid actorUserGuid,
        SectorListQuery query,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        var sectors = dbContext.Setores.AsNoTracking()
            .Where(x => x.UnidadeHospitalar.OrganizacaoId == organizationId);
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            sectors = sectors.Where(x => x.Nome.Contains(search) || x.Sigla.Contains(search));
        }

        if (query.Active.HasValue)
        {
            sectors = sectors.Where(x => x.Ativo == query.Active.Value);
        }

        if (query.UnitGuid.HasValue)
        {
            var unitGuid = query.UnitGuid.Value;
            sectors = sectors.Where(x => x.UnidadeHospitalar.Guid == unitGuid);
        }

        if (query.CategoryGuid.HasValue)
        {
            var categoryGuid = query.CategoryGuid.Value;
            sectors = sectors.Where(x => x.CategoriaSetor.Guid == categoryGuid);
        }

        var total = await sectors.CountAsync(cancellationToken);
        var items = await sectors
            .OrderBy(x => x.UnidadeHospitalar.Nome)
            .ThenBy(x => x.Nome)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new SectorSummaryResponse(
                x.Guid,
                x.Sigla,
                x.Nome,
                x.Ativo,
                x.Assistencial,
                x.PermiteAlocacaoEscala,
                x.PermiteAtuacaoCompartilhada,
                new HospitalUnitReferenceResponse(x.UnidadeHospitalar.Guid, x.UnidadeHospitalar.Nome),
                new SectorCategoryReferenceResponse(x.CategoriaSetor.Guid, x.CategoriaSetor.Nome)))
            .ToListAsync(cancellationToken);

        return new SectorPageResponse(items, query.Page, query.PageSize, total);
    }

    public async Task<SectorResponse?> GetSectorAsync(
        Guid actorUserGuid,
        Guid sectorGuid,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        return await BuildSectorResponseAsync(sectorGuid, organizationId, cancellationToken);
    }

    public async Task<SectorResponse> CreateSectorAsync(
        CreateSectorRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(context.ActorUserGuid, cancellationToken);

        // A criação de um setor ativo participa do mesmo protocolo de lock da
        // categoria usado pela inativação de categoria. O id abaixo só compõe o
        // recurso do lock; a categoria é relida e revalidada (ativa/não excluída)
        // dentro da transação, já sob o lock, por ResolveCategoryIdAsync.
        var categoryLockId = await ResolveCategoryLockIdAsync(request.CategoryGuid, cancellationToken);

        var sectorGuid = await ExecuteMutationAsync(async () =>
        {
            var unit = await ResolveUnitAsync(request.UnitGuid, organizationId, cancellationToken);
            var categoryId = await ResolveCategoryIdAsync(request.CategoryGuid, cancellationToken);
            var responsibleId = await ResolveResponsibleIdAsync(
                request.ResponsibleEmployeeGuid, organizationId, cancellationToken);
            await EnsureSectorNameIsAvailableAsync(unit.Id, request.Name, null, cancellationToken);
            await EnsureSectorSiglaIsAvailableAsync(unit.Id, request.Sigla, null, cancellationToken);

            var servedUnits = await ResolveServedUnitsAsync(
                request.ServedUnits ?? [], unit.Id, organizationId, cancellationToken);
            if (servedUnits.Count > 0 && !request.AllowsSharedActing)
            {
                throw new DomainException(
                    "Habilite a atuação compartilhada para informar unidades atendidas.");
            }

            var now = timeProvider.GetUtcNow();
            var sector = new Setor(
                Guid.NewGuid(),
                unit.Id,
                categoryId,
                request.Name,
                request.Sigla,
                request.Description,
                request.InternalLocation,
                request.Extension,
                request.Email,
                responsibleId,
                request.CareRelated,
                request.AllowsScheduleAllocation,
                request.AllowsSharedActing,
                now);
            dbContext.Setores.Add(sector);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var served in servedUnits)
            {
                dbContext.SetoresUnidadesAtendidas.Add(new SetorUnidadeAtendida(
                    Guid.NewGuid(), sector.Id, served.Id, served.StartDate, now));
            }

            var data = new
            {
                sectorGuid = sector.Guid,
                unitGuid = unit.Guid,
                sector.Sigla,
                sector.Nome,
                categoryGuid = request.CategoryGuid,
                servedUnitGuids = servedUnits.Select(x => x.Guid)
            };
            AddAuditAndOutbox("SetorCriado", "Setor", "CRIADO", sector.Guid, context, null, data, now);
            return sector.Guid;
        },
        cancellationToken,
        categoryLockId is { } lockCategoryId
            ? ct => AcquireLocksAsync(ct, SectorCategoryLockResource(lockCategoryId))
            : null);

        return await BuildSectorResponseAsync(sectorGuid, organizationId, cancellationToken)
            ?? throw new InvalidOperationException("O setor persistido não pôde ser recuperado.");
    }

    public async Task<SectorResponse?> UpdateSectorAsync(
        Guid sectorGuid,
        UpdateSectorRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(context.ActorUserGuid, cancellationToken);

        // Dois recursos, adquiridos SEMPRE na ordem determinística categoria -> setor
        // (AcquireLocksAsync ordena e deduplica):
        //   categoria destino: serializa com a inativação da categoria alvo, mantendo
        //     "setor ativo => categoria ativa" mesmo quando o update repõe/troca a
        //     categoria de um setor que permanece ativo.
        //   setor: serializa com AddSectorServedUnitAsync ("desabilitar atuação
        //     compartilhada" x "adicionar unidade atendida").
        var sectorLockId = await dbContext.Setores.AsNoTracking()
            .Where(x => x.Guid == sectorGuid && x.UnidadeHospitalar.OrganizacaoId == organizationId)
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var categoryLockId = await ResolveCategoryLockIdAsync(request.CategoryGuid, cancellationToken);

        var found = await ExecuteMutationAsync(async () =>
        {
            var sector = await dbContext.Setores
                .SingleOrDefaultAsync(
                    x => x.Guid == sectorGuid && x.UnidadeHospitalar.OrganizacaoId == organizationId,
                    cancellationToken);
            if (sector is null)
            {
                return false;
            }

            var categoryId = await ResolveCategoryIdAsync(request.CategoryGuid, cancellationToken);
            var responsibleId = await ResolveResponsibleIdAsync(
                request.ResponsibleEmployeeGuid, organizationId, cancellationToken);
            await EnsureSectorNameIsAvailableAsync(
                sector.UnidadeHospitalarId, request.Name, sector.Id, cancellationToken);
            await EnsureSectorSiglaIsAvailableAsync(
                sector.UnidadeHospitalarId, request.Sigla, sector.Id, cancellationToken);

            if (!request.AllowsSharedActing
                && sector.PermiteAtuacaoCompartilhada
                && await dbContext.SetoresUnidadesAtendidas.AnyAsync(
                    x => x.SetorId == sector.Id && x.Ativo, cancellationToken))
            {
                throw new DomainException(
                    "Encerre as unidades atendidas antes de desabilitar a atuação compartilhada.");
            }

            var before = Snapshot(sector);
            var now = timeProvider.GetUtcNow();
            if (sector.Atualizar(
                    categoryId,
                    request.Name,
                    request.Sigla,
                    request.Description,
                    request.InternalLocation,
                    request.Extension,
                    request.Email,
                    responsibleId,
                    request.CareRelated,
                    request.AllowsScheduleAllocation,
                    request.AllowsSharedActing,
                    now))
            {
                AddAuditAndOutbox(
                    "SetorAtualizado", "Setor", "ATUALIZADO", sector.Guid,
                    context, before, Snapshot(sector), now);
            }

            return true;
        },
        cancellationToken,
        ct => AcquireLocksAsync(
            ct,
            categoryLockId is { } lockCategoryId ? SectorCategoryLockResource(lockCategoryId) : string.Empty,
            sectorLockId is { } lockSectorId ? SectorLockResource(lockSectorId) : string.Empty));

        return found ? await BuildSectorResponseAsync(sectorGuid, organizationId, cancellationToken) : null;
    }

    public async Task<SectorResponse?> ChangeSectorStatusAsync(
        Guid sectorGuid,
        bool active,
        SectorOperationContext context,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(context.ActorUserGuid, cancellationToken);

        // A reativação serializa com a inativação da categoria no mesmo recurso lógico
        // (a categoria atual do setor), fechando a janela entre "categoria está ativa?"
        // e "existe setor ativo nesta categoria?".
        var categoryLockId = active
            ? await dbContext.Setores.AsNoTracking()
                .Where(x => x.Guid == sectorGuid && x.UnidadeHospitalar.OrganizacaoId == organizationId)
                .Select(x => (long?)x.CategoriaSetorId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var found = await ExecuteMutationAsync(async () =>
        {
            var sector = await dbContext.Setores
                .SingleOrDefaultAsync(
                    x => x.Guid == sectorGuid && x.UnidadeHospitalar.OrganizacaoId == organizationId,
                    cancellationToken);
            if (sector is null)
            {
                return false;
            }

            if (sector.Ativo == active)
            {
                return true;
            }

            if (active && !await dbContext.CategoriasSetores.AnyAsync(
                    x => x.Id == sector.CategoriaSetorId && x.Ativo, cancellationToken))
            {
                // Reativar um setor cuja categoria foi inativada recriaria a combinação
                // proibida (setor ativo + categoria inativa) que as demais validações já
                // impedem na criação/atualização e na inativação da categoria.
                throw new DomainException(
                    "A categoria associada ao setor está inativa e impede sua reativação.");
            }

            var before = Snapshot(sector);
            var now = timeProvider.GetUtcNow();
            if (active)
            {
                sector.Reativar(now);
            }
            else
            {
                sector.Inativar(now);
            }

            AddAuditAndOutbox(
                active ? "SetorReativado" : "SetorInativado",
                "Setor",
                active ? "REATIVADO" : "INATIVADO",
                sector.Guid,
                context,
                before,
                Snapshot(sector),
                now);
            return true;
        },
        cancellationToken,
        categoryLockId is { } lockCategoryId
            ? ct => AcquireLocksAsync(ct, SectorCategoryLockResource(lockCategoryId))
            : null);

        return found ? await BuildSectorResponseAsync(sectorGuid, organizationId, cancellationToken) : null;
    }

    public async Task<SectorServedUnitResponse?> AddSectorServedUnitAsync(
        Guid sectorGuid,
        AddSectorServedUnitRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(context.ActorUserGuid, cancellationToken);

        // Serializa com UpdateSectorAsync (desabilitar atuação compartilhada) no
        // recurso lógico do setor: garante que, ao criar o vínculo, o valor de
        // PermiteAtuacaoCompartilhada lido dentro da transação já reflita qualquer
        // desabilitação concorrente commitada.
        var sectorLockId = await dbContext.Setores.AsNoTracking()
            .Where(x => x.Guid == sectorGuid && x.UnidadeHospitalar.OrganizacaoId == organizationId)
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var relationshipGuid = await ExecuteMutationAsync(async () =>
        {
            var sector = await dbContext.Setores
                .SingleOrDefaultAsync(
                    x => x.Guid == sectorGuid && x.UnidadeHospitalar.OrganizacaoId == organizationId,
                    cancellationToken);
            if (sector is null)
            {
                return (Guid?)null;
            }

            if (!sector.Ativo)
            {
                throw new DomainException("Não é possível adicionar unidades atendidas a um setor inativo.");
            }

            if (!sector.PermiteAtuacaoCompartilhada)
            {
                throw new DomainException(
                    "O setor não permite atuação compartilhada entre unidades hospitalares.");
            }

            var unit = await ResolveUnitAsync(request.UnitGuid, organizationId, cancellationToken);
            if (unit.Id == sector.UnidadeHospitalarId)
            {
                throw new DomainException(
                    "A unidade principal do setor não pode ser cadastrada como unidade atendida.");
            }

            if (await dbContext.SetoresUnidadesAtendidas.AnyAsync(
                    x => x.SetorId == sector.Id && x.UnidadeHospitalarId == unit.Id && x.Ativo,
                    cancellationToken))
            {
                throw new DomainException("O setor já atende esta unidade hospitalar.");
            }

            if (await dbContext.SetoresUnidadesAtendidas.AnyAsync(
                    x => x.SetorId == sector.Id
                        && x.UnidadeHospitalarId == unit.Id
                        && x.DataFim.HasValue
                        && x.DataFim.Value >= request.StartDate,
                    cancellationToken))
            {
                throw new DomainException("O novo período de atendimento não pode sobrepor um período anterior.");
            }

            var now = timeProvider.GetUtcNow();
            var relationship = new SetorUnidadeAtendida(
                Guid.NewGuid(), sector.Id, unit.Id, request.StartDate, now);
            dbContext.SetoresUnidadesAtendidas.Add(relationship);
            var data = new { relationshipGuid = relationship.Guid, unitGuid = unit.Guid, request.StartDate };
            AddAuditAndOutbox(
                "SetorUnidadeAtendidaAdicionada", "Setor", "UNIDADE_ATENDIDA_ADICIONADA",
                sector.Guid, context, null, data, now);
            return relationship.Guid;
        },
        cancellationToken,
        sectorLockId is { } lockSectorId
            ? ct => AcquireLocksAsync(ct, SectorLockResource(lockSectorId))
            : null);

        return relationshipGuid.HasValue
            ? await GetServedUnitAsync(relationshipGuid.Value, cancellationToken)
            : null;
    }

    public async Task<bool?> EndSectorServedUnitAsync(
        Guid sectorGuid,
        Guid relationshipGuid,
        DateOnly endDate,
        SectorOperationContext context,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(context.ActorUserGuid, cancellationToken);
        return await ExecuteMutationAsync(async () =>
        {
            var relationship = await dbContext.SetoresUnidadesAtendidas
                .Include(x => x.Setor)
                .Include(x => x.UnidadeHospitalar)
                .SingleOrDefaultAsync(
                    x => x.Guid == relationshipGuid
                        && x.Setor.Guid == sectorGuid
                        && x.Setor.UnidadeHospitalar.OrganizacaoId == organizationId,
                    cancellationToken);
            if (relationship is null)
            {
                return (bool?)null;
            }

            var now = timeProvider.GetUtcNow();
            if (!relationship.Encerrar(endDate, now))
            {
                return false;
            }

            var data = new
            {
                relationshipGuid = relationship.Guid,
                unitGuid = relationship.UnidadeHospitalar.Guid,
                endDate
            };
            AddAuditAndOutbox(
                "SetorUnidadeAtendidaEncerrada", "Setor", "UNIDADE_ATENDIDA_ENCERRADA",
                relationship.Setor.Guid, context, null, data, now);
            return true;
        }, cancellationToken);
    }

    public async Task<SectorCategoryPageResponse> ListSectorCategoriesAsync(
        SectorCategoryListQuery query,
        CancellationToken cancellationToken)
    {
        var categories = dbContext.CategoriasSetores.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            categories = categories.Where(x => x.Nome.Contains(search));
        }

        if (query.Active.HasValue)
        {
            categories = categories.Where(x => x.Ativo == query.Active.Value);
        }

        var total = await categories.CountAsync(cancellationToken);
        var items = await categories
            .OrderBy(x => x.Nome)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new SectorCategoryResponse(
                x.Guid, x.Nome, x.Descricao, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .ToListAsync(cancellationToken);
        return new SectorCategoryPageResponse(items, query.Page, query.PageSize, total);
    }

    public Task<SectorCategoryResponse?> GetSectorCategoryAsync(
        Guid categoryGuid,
        CancellationToken cancellationToken) =>
        dbContext.CategoriasSetores.AsNoTracking()
            .Where(x => x.Guid == categoryGuid)
            .Select(x => new SectorCategoryResponse(
                x.Guid, x.Nome, x.Descricao, x.Ativo, x.DataCriacao, x.DataAtualizacao))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SectorCategoryResponse> CreateSectorCategoryAsync(
        CreateSectorCategoryRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken)
    {
        var categoryGuid = await ExecuteMutationAsync(async () =>
        {
            await EnsureCategoryNameIsAvailableAsync(request.Name, null, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var category = new CategoriaSetor(Guid.NewGuid(), request.Name, request.Description, now);
            dbContext.CategoriasSetores.Add(category);
            AddAuditAndOutbox(
                "CategoriaSetorCriada", "CategoriaSetor", "CRIADA", category.Guid,
                context, null, Snapshot(category), now);
            return category.Guid;
        }, cancellationToken);

        return await GetSectorCategoryAsync(categoryGuid, cancellationToken)
            ?? throw new InvalidOperationException("A categoria de setor persistida não pôde ser recuperada.");
    }

    public async Task<SectorCategoryResponse?> UpdateSectorCategoryAsync(
        Guid categoryGuid,
        UpdateSectorCategoryRequest request,
        SectorOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var category = await dbContext.CategoriasSetores
                .SingleOrDefaultAsync(x => x.Guid == categoryGuid, cancellationToken);
            if (category is null)
            {
                return false;
            }

            await EnsureCategoryNameIsAvailableAsync(request.Name, category.Id, cancellationToken);
            var before = Snapshot(category);
            var now = timeProvider.GetUtcNow();
            if (category.Atualizar(request.Name, request.Description, now))
            {
                AddAuditAndOutbox(
                    "CategoriaSetorAtualizada", "CategoriaSetor", "ATUALIZADA", category.Guid,
                    context, before, Snapshot(category), now);
            }

            return true;
        }, cancellationToken);

        return found ? await GetSectorCategoryAsync(categoryGuid, cancellationToken) : null;
    }

    public async Task<SectorCategoryResponse?> ChangeSectorCategoryStatusAsync(
        Guid categoryGuid,
        bool active,
        SectorOperationContext context,
        CancellationToken cancellationToken)
    {
        // A inativação serializa com a reativação de setor no mesmo recurso lógico
        // (esta categoria), fechando a janela entre "existe setor ativo?" e a
        // reativação de um setor desta categoria.
        var categoryLockId = !active
            ? await dbContext.CategoriasSetores.AsNoTracking()
                .Where(x => x.Guid == categoryGuid)
                .Select(x => (long?)x.Id)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var found = await ExecuteMutationAsync(async () =>
        {
            var category = await dbContext.CategoriasSetores
                .SingleOrDefaultAsync(x => x.Guid == categoryGuid, cancellationToken);
            if (category is null)
            {
                return false;
            }

            if (category.Ativo == active)
            {
                return true;
            }

            if (!active && await dbContext.Setores.AnyAsync(
                    x => x.CategoriaSetorId == category.Id && x.Ativo, cancellationToken))
            {
                throw new DomainException(
                    "A categoria possui setores ativos e não pode ser inativada.");
            }

            var before = Snapshot(category);
            var now = timeProvider.GetUtcNow();
            if (active)
            {
                category.Reativar(now);
            }
            else
            {
                category.Inativar(now);
            }

            AddAuditAndOutbox(
                active ? "CategoriaSetorReativada" : "CategoriaSetorInativada",
                "CategoriaSetor",
                active ? "REATIVADA" : "INATIVADA",
                category.Guid,
                context,
                before,
                Snapshot(category),
                now);
            return true;
        },
        cancellationToken,
        categoryLockId is { } lockCategoryId
            ? ct => AcquireLocksAsync(ct, SectorCategoryLockResource(lockCategoryId))
            : null);

        return found ? await GetSectorCategoryAsync(categoryGuid, cancellationToken) : null;
    }

    private async Task<SectorResponse?> BuildSectorResponseAsync(
        Guid sectorGuid,
        long organizationId,
        CancellationToken cancellationToken)
    {
        var sector = await dbContext.Setores.AsNoTracking()
            .Where(x => x.Guid == sectorGuid && x.UnidadeHospitalar.OrganizacaoId == organizationId)
            .Select(x => new
            {
                x.Id,
                x.Guid,
                x.Sigla,
                x.Nome,
                x.Ativo,
                Unit = new HospitalUnitReferenceResponse(x.UnidadeHospitalar.Guid, x.UnidadeHospitalar.Nome),
                Category = new SectorCategoryReferenceResponse(x.CategoriaSetor.Guid, x.CategoriaSetor.Nome),
                x.Descricao,
                x.LocalizacaoInterna,
                x.Ramal,
                x.Email,
                x.ResponsavelFuncionarioId,
                x.Assistencial,
                x.PermiteAlocacaoEscala,
                x.PermiteAtuacaoCompartilhada,
                x.DataCriacao,
                x.DataAtualizacao
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (sector is null)
        {
            return null;
        }

        SectorResponsibleResponse? responsible = null;
        if (sector.ResponsavelFuncionarioId is long responsibleId)
        {
            responsible = await dbContext.Funcionarios.AsNoTracking()
                .Where(x => x.Id == responsibleId)
                .Select(x => new SectorResponsibleResponse(x.Guid, x.Nome, x.Matricula ?? string.Empty))
                .SingleOrDefaultAsync(cancellationToken);
        }

        var servedUnits = await dbContext.SetoresUnidadesAtendidas.AsNoTracking()
            .Where(x => x.SetorId == sector.Id)
            .OrderByDescending(x => x.Ativo)
            .ThenByDescending(x => x.DataInicio)
            .Select(x => new SectorServedUnitResponse(
                x.Guid, x.UnidadeHospitalar.Guid, x.UnidadeHospitalar.Nome,
                x.DataInicio, x.DataFim, x.Ativo))
            .ToListAsync(cancellationToken);

        return new SectorResponse(
            sector.Guid,
            sector.Sigla,
            sector.Nome,
            sector.Ativo,
            sector.Unit,
            sector.Category,
            sector.Descricao,
            sector.LocalizacaoInterna,
            sector.Ramal,
            sector.Email,
            responsible,
            sector.Assistencial,
            sector.PermiteAlocacaoEscala,
            sector.PermiteAtuacaoCompartilhada,
            servedUnits,
            sector.DataCriacao,
            sector.DataAtualizacao);
    }

    private Task<SectorServedUnitResponse> GetServedUnitAsync(
        Guid relationshipGuid,
        CancellationToken cancellationToken) =>
        dbContext.SetoresUnidadesAtendidas.AsNoTracking()
            .Where(x => x.Guid == relationshipGuid)
            .Select(x => new SectorServedUnitResponse(
                x.Guid, x.UnidadeHospitalar.Guid, x.UnidadeHospitalar.Nome,
                x.DataInicio, x.DataFim, x.Ativo))
            .SingleAsync(cancellationToken);

    private async Task<UnitReference> ResolveUnitAsync(
        Guid unitGuid,
        long organizationId,
        CancellationToken cancellationToken)
    {
        var unit = await dbContext.UnidadesHospitalares
            .Where(x => x.Guid == unitGuid && x.Ativo && x.Organizacao.Ativo)
            .Select(x => new UnitReference(x.Id, x.Guid, x.OrganizacaoId))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("A unidade hospitalar informada não existe ou está inativa.");

        if (unit.OrganizationId != organizationId)
        {
            throw new DomainException("A unidade hospitalar informada pertence a outra organização.");
        }

        return unit;
    }

    private async Task<long> ResolveCategoryIdAsync(Guid categoryGuid, CancellationToken cancellationToken) =>
        await dbContext.CategoriasSetores
            .Where(x => x.Guid == categoryGuid && x.Ativo)
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new DomainException("A categoria de setor informada não existe ou está inativa.");

    /// <summary>
    /// Id interno da categoria apenas para compor o recurso do lock lógico
    /// (independe de estar ativa — o objetivo é serializar contra uma inativação
    /// concorrente). A validação de existência/ativa acontece dentro da transação,
    /// sob o lock, em <see cref="ResolveCategoryIdAsync"/>.
    /// </summary>
    private Task<long?> ResolveCategoryLockIdAsync(Guid categoryGuid, CancellationToken cancellationToken) =>
        dbContext.CategoriasSetores.AsNoTracking()
            .Where(x => x.Guid == categoryGuid)
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<long?> ResolveResponsibleIdAsync(
        Guid? responsibleEmployeeGuid,
        long organizationId,
        CancellationToken cancellationToken)
    {
        if (responsibleEmployeeGuid is not Guid guid || guid == Guid.Empty)
        {
            return null;
        }

        var responsible = await dbContext.Funcionarios
            .Where(x => x.Guid == guid && x.Ativo)
            .Select(x => new { x.Id, OrganizationId = x.UnidadeContratacao.OrganizacaoId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("O responsável informado não existe ou está inativo.");

        if (responsible.OrganizationId != organizationId)
        {
            throw new DomainException("O responsável informado pertence a outra organização.");
        }

        return responsible.Id;
    }

    private async Task<IReadOnlyCollection<ServedUnitReference>> ResolveServedUnitsAsync(
        IReadOnlyCollection<CreateSectorServedUnitRequest> requests,
        long principalUnitId,
        long organizationId,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        if (requests.Select(x => x.UnitGuid).Distinct().Count() != requests.Count)
        {
            throw new DomainException("A lista de unidades atendidas contém itens duplicados.");
        }

        var dates = requests.ToDictionary(x => x.UnitGuid, x => x.StartDate);
        var persisted = await dbContext.UnidadesHospitalares
            .Where(x => dates.Keys.Contains(x.Guid) && x.Ativo && x.Organizacao.Ativo)
            .Select(x => new { x.Id, x.Guid, x.OrganizacaoId })
            .ToListAsync(cancellationToken);
        if (persisted.Count != requests.Count)
        {
            throw new DomainException("Uma ou mais unidades atendidas não existem ou estão inativas.");
        }

        if (persisted.Any(x => x.OrganizacaoId != organizationId))
        {
            throw new DomainException("As unidades atendidas devem pertencer à organização do setor.");
        }

        if (persisted.Any(x => x.Id == principalUnitId))
        {
            throw new DomainException(
                "A unidade principal do setor não pode ser cadastrada como unidade atendida.");
        }

        return persisted
            .Select(x => new ServedUnitReference(x.Id, x.Guid, dates[x.Guid]))
            .ToList();
    }

    private async Task EnsureSectorNameIsAvailableAsync(
        long unitId,
        string name,
        long? ignoredId,
        CancellationToken cancellationToken)
    {
        var normalized = ChaveNegocio.Normalizar(name);
        if (await dbContext.Setores.AnyAsync(
                x => x.UnidadeHospitalarId == unitId
                    && x.Nome == normalized
                    && (!ignoredId.HasValue || x.Id != ignoredId.Value),
                cancellationToken))
        {
            throw new DuplicateBusinessKeyException(
                "Já existe um setor com este nome nesta unidade hospitalar.", field: "name");
        }
    }

    private async Task EnsureSectorSiglaIsAvailableAsync(
        long unitId,
        string sigla,
        long? ignoredId,
        CancellationToken cancellationToken)
    {
        var normalized = ChaveNegocio.Normalizar(sigla).ToUpperInvariant();
        if (await dbContext.Setores.AnyAsync(
                x => x.UnidadeHospitalarId == unitId
                    && x.Sigla == normalized
                    && (!ignoredId.HasValue || x.Id != ignoredId.Value),
                cancellationToken))
        {
            throw new DuplicateBusinessKeyException(
                "Já existe um setor com esta sigla nesta unidade hospitalar.", field: "sigla");
        }
    }

    private async Task EnsureCategoryNameIsAvailableAsync(
        string name,
        long? ignoredId,
        CancellationToken cancellationToken)
    {
        var normalized = ChaveNegocio.Normalizar(name);
        if (await dbContext.CategoriasSetores.AnyAsync(
                x => x.Nome == normalized && (!ignoredId.HasValue || x.Id != ignoredId.Value),
                cancellationToken))
        {
            throw new DuplicateBusinessKeyException(
                "Já existe uma categoria de setor com este nome.", field: "name");
        }
    }

    private void AddAuditAndOutbox(
        string eventType,
        string entity,
        string action,
        Guid entityGuid,
        SectorOperationContext context,
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
                // O lock lógico opcional é adquirido dentro da transação e liberado só
                // depois do commit (applock com @LockOwner='Transaction'; semáforo em
                // processo para o provider InMemory). Serializa invariantes que duas
                // pré-checagens concorrentes poderiam furar.
                await using var mutationLock = acquireLock is null
                    ? NoopAsyncDisposable.Instance
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
            // O banco é a última barreira de integridade: sob concorrência duas
            // requisições podem passar a pré-checagem e colidir no índice único. A
            // tradução identifica qual chave de negócio colidiu para devolver o
            // `field` correto — nome, sigla ou (vínculo ativo de unidade atendida)
            // a mesma regra de domínio da pré-checagem. Nada do texto localizado do
            // SQL Server é propagado ao cliente.
            throw TranslateUniqueViolation(sqlException, exception);
        }
    }

    /// <summary>
    /// Índices únicos deste serviço cuja violação (SQL Server 2601/2627) corresponde
    /// a uma chave de negócio pública. O nome do índice é um identificador estável do
    /// schema (não varia com o idioma do servidor) e é usado apenas aqui — nunca
    /// exposto ao cliente.
    /// </summary>
    private static readonly (string IndexName, string Field, string Message)[] SectorUniqueBusinessKeys =
    [
        ("IX_Setores_UnidadeHospitalarId_Nome", "name",
            "Já existe um setor com este nome nesta unidade hospitalar."),
        ("IX_Setores_UnidadeHospitalarId_Sigla", "sigla",
            "Já existe um setor com esta sigla nesta unidade hospitalar."),
        ("IX_CategoriasSetores_Nome", "name",
            "Já existe uma categoria de setor com este nome."),
    ];

    private const string ServedUnitActiveUniqueIndex =
        "IX_SetoresUnidadesAtendidas_SetorId_UnidadeHospitalarId";

    private static Exception TranslateUniqueViolation(SqlException sqlException, DbUpdateException source)
    {
        var violatedIndex = FindViolatedIndex(sqlException);

        if (violatedIndex == ServedUnitActiveUniqueIndex)
        {
            // Vínculo ativo duplicado: mesma regra e mesmo status (422) da
            // pré-checagem, sem inventar um `field` de contrato inexistente.
            return new DomainException("O setor já atende esta unidade hospitalar.");
        }

        foreach (var (indexName, field, message) in SectorUniqueBusinessKeys)
        {
            if (violatedIndex == indexName)
            {
                return new DuplicateBusinessKeyException(
                    message, field, source, sqlException.Number);
            }
        }

        // Sem classificação segura: conflito genérico de chave de negócio, sem
        // `field` — nunca "name" por padrão.
        return new DuplicateBusinessKeyException(
            "Já existe um registro com esta chave de negócio.",
            field: null,
            innerException: source,
            sqlErrorNumber: sqlException.Number);
    }

    private static string? FindViolatedIndex(SqlException sqlException)
    {
        // Só reconhecemos nomes da allowlist como substring do texto do erro
        // 2601/2627 — sem regex e sem interpretar a prosa localizada do banco.
        foreach (SqlError error in sqlException.Errors)
        {
            foreach (var (indexName, _, _) in SectorUniqueBusinessKeys)
            {
                if (error.Message.Contains(indexName, StringComparison.Ordinal))
                {
                    return indexName;
                }
            }

            if (error.Message.Contains(ServedUnitActiveUniqueIndex, StringComparison.Ordinal))
            {
                return ServedUnitActiveUniqueIndex;
            }
        }

        return null;
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    // Locks lógicos por recurso — mesmo padrão de AuthenticationService /
    // PermissionAdministrationService. Dois namespaces:
    //   sge:setor:categoria:{id}  (tier 0) protege a invariável "setor ativo =>
    //                             categoria ativa" em create / update / reativação
    //                             de setor e na inativação de categoria.
    //   sge:setor:{id}            (tier 1) protege "AllowsSharedActing=false =>
    //                             nenhuma unidade atendida ativa".
    // Quando uma operação precisa dos dois (UpdateSectorAsync), a ORDEM ÚNICA E
    // DETERMINÍSTICA é: categoria (tier 0) antes de setor (tier 1). AcquireLocksAsync
    // ordena, deduplica e libera em ordem reversa — nenhum fluxo pode inverter.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> InProcessLocks = new();

    private static readonly IComparer<string> LockResourceOrder = Comparer<string>.Create(
        static (left, right) =>
        {
            var tierComparison = LockResourceTier(left).CompareTo(LockResourceTier(right));
            return tierComparison != 0 ? tierComparison : string.CompareOrdinal(left, right);
        });

    private static int LockResourceTier(string resource) =>
        resource.StartsWith("sge:setor:categoria:", StringComparison.Ordinal) ? 0 : 1;

    private static string SectorCategoryLockResource(long categoriaSetorId) =>
        $"sge:setor:categoria:{categoriaSetorId.ToString(CultureInfo.InvariantCulture)}";

    private static string SectorLockResource(long setorId) =>
        $"sge:setor:{setorId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Adquire um ou mais locks lógicos em ordem determinística (categoria antes de
    /// setor), sem duplicar recursos iguais. Deve ser chamado dentro da transação da
    /// unidade de trabalho.
    /// </summary>
    private async Task<IAsyncDisposable> AcquireLocksAsync(
        CancellationToken cancellationToken,
        params string[] resources)
    {
        var ordered = resources
            .Where(static resource => !string.IsNullOrEmpty(resource))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static resource => resource, LockResourceOrder)
            .ToArray();
        if (ordered.Length == 0)
        {
            return NoopAsyncDisposable.Instance;
        }

        if (!dbContext.Database.IsSqlServer())
        {
            var acquired = new List<SemaphoreSlim>(ordered.Length);
            try
            {
                foreach (var resource in ordered)
                {
                    var semaphore = InProcessLocks.GetOrAdd(resource, static _ => new SemaphoreSlim(1, 1));
                    await semaphore.WaitAsync(cancellationToken);
                    acquired.Add(semaphore);
                }

                return new SemaphoreCollectionReleaser(acquired);
            }
            catch
            {
                for (var index = acquired.Count - 1; index >= 0; index--)
                {
                    acquired[index].Release();
                }

                throw;
            }
        }

        foreach (var resource in ordered)
        {
            await AcquireApplockAsync(resource, cancellationToken);
        }

        // applock com @LockOwner='Transaction' é liberado no commit/rollback da
        // transação da própria unidade de trabalho — nada a liberar manualmente.
        return NoopAsyncDisposable.Instance;
    }

    private async Task AcquireApplockAsync(string resource, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            SELECT @result;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = resource;
        command.Parameters.Add(parameter);
        var result = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (result < 0)
        {
            // Timeout/deadlock na obtenção do lock: erro controlado e genérico
            // (TimeoutException -> HTTP 503 pelo GlobalExceptionHandler), sem expor
            // recurso interno, SQL ou stack trace.
            throw new TimeoutException(
                "Não foi possível serializar a operação de setor no momento. Tente novamente.");
        }
    }

    private async Task<long> GetActorOrganizationIdAsync(
        Guid actorUserGuid,
        CancellationToken cancellationToken) =>
        await dbContext.Usuarios.AsNoTracking()
            .Where(x => x.Guid == actorUserGuid
                && x.Ativo
                && x.FuncionarioId.HasValue
                && x.Funcionario!.Ativo)
            .Select(x => (long?)x.Funcionario!.UnidadeContratacao.OrganizacaoId)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new OrganizationAccessDeniedException();

    private static object Snapshot(Setor sector) => new
    {
        sector.Guid,
        sector.Sigla,
        sector.Nome,
        sector.CategoriaSetorId,
        sector.Descricao,
        sector.LocalizacaoInterna,
        sector.Ramal,
        sector.Email,
        sector.ResponsavelFuncionarioId,
        sector.Assistencial,
        sector.PermiteAlocacaoEscala,
        sector.PermiteAtuacaoCompartilhada,
        sector.Ativo
    };

    private static object Snapshot(CategoriaSetor category) => new
    {
        category.Guid,
        category.Nome,
        category.Descricao,
        category.Ativo
    };

    private sealed record UnitReference(long Id, Guid Guid, long OrganizationId);
    private sealed record ServedUnitReference(long Id, Guid Guid, DateOnly StartDate);

    private sealed class SemaphoreCollectionReleaser(IReadOnlyList<SemaphoreSlim> semaphores) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            for (var index = semaphores.Count - 1; index >= 0; index--)
            {
                semaphores[index].Release();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoopAsyncDisposable Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
