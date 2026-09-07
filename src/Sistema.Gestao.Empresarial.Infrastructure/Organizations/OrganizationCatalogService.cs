using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Organizations;

public sealed class OrganizationCatalogService(AppDbContext dbContext) : IOrganizationCatalogService
{
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
            sectors = sectors.Where(x => x.Nome.Contains(search));
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

        var total = await sectors.CountAsync(cancellationToken);
        var items = await sectors
            .OrderBy(x => x.UnidadeHospitalar.Nome)
            .ThenBy(x => x.Nome)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new SectorResponse(
                x.Guid,
                x.Nome,
                x.Ativo,
                new HospitalUnitReferenceResponse(
                    x.UnidadeHospitalar.Guid,
                    x.UnidadeHospitalar.Nome),
                x.DataCriacao,
                x.DataAtualizacao))
            .ToListAsync(cancellationToken);

        return new SectorPageResponse(items, query.Page, query.PageSize, total);
    }

    public async Task<SectorResponse?> GetSectorAsync(
        Guid actorUserGuid,
        Guid sectorGuid,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        return await dbContext.Setores.AsNoTracking()
            .Where(x => x.Guid == sectorGuid
                && x.UnidadeHospitalar.OrganizacaoId == organizationId)
            .Select(x => new SectorResponse(
                x.Guid,
                x.Nome,
                x.Ativo,
                new HospitalUnitReferenceResponse(
                    x.UnidadeHospitalar.Guid,
                    x.UnidadeHospitalar.Nome),
                x.DataCriacao,
                x.DataAtualizacao))
            .SingleOrDefaultAsync(cancellationToken);
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
}
