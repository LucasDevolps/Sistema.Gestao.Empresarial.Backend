using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.SqlClient;
using Sistema.Gestao.Empresarial.Application.Employees;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Employees;

public sealed class EmployeeService(AppDbContext dbContext, TimeProvider timeProvider) : IEmployeeService
{
    private const string Producer = "Sistema.Gestao.Empresarial.Api";

    public async Task<EmployeePageResponse> ListAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken)
    {
        var employees = dbContext.Funcionarios.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            employees = employees.Where(x =>
                x.Nome.Contains(search) || x.Email.Contains(search) || x.Matricula.Contains(search));
        }

        if (query.Active.HasValue)
        {
            employees = employees.Where(x => x.Ativo == query.Active.Value);
        }

        if (query.ActingUnitGuid.HasValue)
        {
            var unitGuid = query.ActingUnitGuid.Value;
            employees = employees.Where(employee => dbContext.FuncionariosUnidadesAtuacao.Any(actingUnit =>
                actingUnit.FuncionarioId == employee.Id
                && actingUnit.Ativo
                && actingUnit.UnidadeHospitalar.Guid == unitGuid));
        }

        var total = await employees.CountAsync(cancellationToken);
        var items = await employees
            .OrderBy(x => x.Nome)
            .ThenBy(x => x.Matricula)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new EmployeeSummaryResponse(
                x.Guid,
                x.Matricula,
                x.Nome,
                x.Email,
                x.Ativo,
                new EmployeeReferenceResponse(x.Profissao.Guid, x.Profissao.Nome),
                new EmployeeReferenceResponse(x.Cargo.Guid, x.Cargo.Nome),
                new EmployeeLevelResponse(x.Nivel.Guid, x.Nivel.Codigo, x.Nivel.Nome),
                new EmployeeReferenceResponse(x.UnidadeContratacao.Guid, x.UnidadeContratacao.Nome)))
            .ToListAsync(cancellationToken);

        return new EmployeePageResponse(items, query.Page, query.PageSize, total);
    }

    public Task<EmployeeResponse?> GetAsync(Guid employeeGuid, CancellationToken cancellationToken) =>
        BuildResponseAsync(employeeGuid, cancellationToken);

    public async Task<EmployeeResponse> CreateAsync(
        CreateEmployeeRequest request,
        EmployeeOperationContext context,
        CancellationToken cancellationToken)
    {
        var employeeGuid = await ExecuteMutationAsync(async () =>
        {
            await EnsureEmailIsAvailableAsync(request.Email, null, cancellationToken);
            var references = await ResolveProfessionalReferencesAsync(
                request.ProfessionGuid, request.PositionGuid, request.LevelGuid, cancellationToken);
            var hiringUnit = await ResolveUnitAsync(request.HiringUnitGuid, cancellationToken);
            var actingUnits = await ResolveActingUnitsAsync(
                request.ActingUnits ?? [], hiringUnit.OrganizationId, cancellationToken);
            var sectors = await ResolveSectorsAsync(
                request.Sectors ?? [], hiringUnit.OrganizationId, actingUnits.Select(x => x.Id).ToHashSet(), cancellationToken);
            if (actingUnits.Any(x => x.StartDate < request.AdmissionDate))
            {
                throw new DomainException("A atuação em uma unidade não pode começar antes da admissão.");
            }

            if (sectors.Any(x =>
                    x.StartDate < request.AdmissionDate
                    || x.StartDate < actingUnits.Single(unit => unit.Id == x.UnitId).StartDate))
            {
                throw new DomainException(
                    "A atuação em um setor não pode começar antes da admissão ou da atuação na unidade.");
            }

            var now = timeProvider.GetUtcNow();
            var employee = new Funcionario(
                Guid.NewGuid(), request.Name, request.Email, request.Phone,
                references.ProfessionId, references.PositionId, references.LevelId,
                hiringUnit.Id, request.AdmissionDate, now);
            dbContext.Funcionarios.Add(employee);

            // O primeiro flush obtém Id e matrícula da SEQUENCE; tudo permanece na mesma transação.
            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var item in actingUnits)
            {
                dbContext.FuncionariosUnidadesAtuacao.Add(new FuncionarioUnidadeAtuacao(
                    Guid.NewGuid(), employee.Id, item.Id, item.StartDate, now));
            }

            foreach (var item in sectors)
            {
                dbContext.FuncionariosSetores.Add(new FuncionarioSetor(
                    Guid.NewGuid(), employee.Id, item.Id, item.StartDate, now));
            }

            var data = new
            {
                employeeGuid = employee.Guid,
                registrationNumber = employee.Matricula,
                hiringUnitGuid = hiringUnit.Guid,
                actingUnitGuids = actingUnits.Select(x => x.Guid),
                sectorGuids = sectors.Select(x => x.Guid)
            };
            AddAuditAndOutbox("FuncionarioCriado", "CRIADO", employee.Guid, context, null, data, now);
            return employee.Guid;
        }, cancellationToken);

        return await GetRequiredAsync(employeeGuid, cancellationToken);
    }

    public async Task<EmployeeResponse?> UpdateAsync(
        Guid employeeGuid,
        UpdateEmployeeRequest request,
        EmployeeOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var employee = await dbContext.Funcionarios
                .Include(x => x.Profissao)
                .Include(x => x.Cargo)
                .Include(x => x.Nivel)
                .SingleOrDefaultAsync(x => x.Guid == employeeGuid, cancellationToken);
            if (employee is null)
            {
                return false;
            }

            await EnsureEmailIsAvailableAsync(request.Email, employee.Id, cancellationToken);
            var references = await ResolveProfessionalReferencesAsync(
                request.ProfessionGuid, request.PositionGuid, request.LevelGuid, cancellationToken);
            var before = new
            {
                name = employee.Nome,
                employee.Email,
                employee.Telefone,
                professionGuid = employee.Profissao.Guid,
                positionGuid = employee.Cargo.Guid,
                levelGuid = employee.Nivel.Guid
            };
            var now = timeProvider.GetUtcNow();
            if (!employee.AtualizarDados(
                    request.Name, request.Email, request.Phone,
                    references.ProfessionId, references.PositionId, references.LevelId, now))
            {
                return true;
            }

            var after = new
            {
                name = employee.Nome,
                employee.Email,
                employee.Telefone,
                request.ProfessionGuid,
                request.PositionGuid,
                request.LevelGuid
            };
            AddAuditAndOutbox("FuncionarioAtualizado", "ATUALIZADO", employee.Guid, context, before, after, now);
            return true;
        }, cancellationToken);

        return found ? await BuildResponseAsync(employeeGuid, cancellationToken) : null;
    }

    public async Task<EmployeeResponse?> ChangeStatusAsync(
        Guid employeeGuid,
        bool active,
        EmployeeOperationContext context,
        CancellationToken cancellationToken)
    {
        var found = await ExecuteMutationAsync(async () =>
        {
            var employee = await dbContext.Funcionarios
                .SingleOrDefaultAsync(x => x.Guid == employeeGuid, cancellationToken);
            if (employee is null)
            {
                return false;
            }

            if (employee.Ativo == active)
            {
                return true;
            }

            var now = timeProvider.GetUtcNow();
            if (active)
            {
                employee.Reativar(now);
            }
            else
            {
                employee.Inativar(now);
            }

            var eventType = active ? "FuncionarioReativado" : "FuncionarioInativado";
            var action = active ? "REATIVADO" : "INATIVADO";
            AddAuditAndOutbox(
                eventType, action, employee.Guid, context,
                new { active = !active }, new { active }, now);
            return true;
        }, cancellationToken);

        return found ? await BuildResponseAsync(employeeGuid, cancellationToken) : null;
    }

    public async Task<EmployeeActingUnitResponse?> AddActingUnitAsync(
        Guid employeeGuid,
        AddEmployeeActingUnitRequest request,
        EmployeeOperationContext context,
        CancellationToken cancellationToken)
    {
        var relationshipGuid = await ExecuteMutationAsync(async () =>
        {
            var employee = await dbContext.Funcionarios
                .Include(x => x.UnidadeContratacao)
                .SingleOrDefaultAsync(x => x.Guid == employeeGuid, cancellationToken);
            if (employee is null)
            {
                return (Guid?)null;
            }

            EnsureEmployeeIsActive(employee);
            var unit = await ResolveUnitAsync(request.UnitGuid, cancellationToken);
            EnsureSameOrganization(employee.UnidadeContratacao.OrganizacaoId, unit.OrganizationId);
            if (request.StartDate < employee.DataAdmissao)
            {
                throw new DomainException("A atuação em uma unidade não pode começar antes da admissão.");
            }

            if (await dbContext.FuncionariosUnidadesAtuacao.AnyAsync(x =>
                    x.FuncionarioId == employee.Id && x.UnidadeHospitalarId == unit.Id && x.Ativo,
                    cancellationToken))
            {
                throw new DomainException("O funcionário já possui vínculo ativo com a unidade informada.");
            }

            if (await dbContext.FuncionariosUnidadesAtuacao.AnyAsync(x =>
                    x.FuncionarioId == employee.Id
                    && x.UnidadeHospitalarId == unit.Id
                    && x.DataFim.HasValue
                    && x.DataFim.Value >= request.StartDate,
                    cancellationToken))
            {
                throw new DomainException("O novo vínculo de atuação não pode sobrepor um período anterior.");
            }

            var now = timeProvider.GetUtcNow();
            var relationship = new FuncionarioUnidadeAtuacao(
                Guid.NewGuid(), employee.Id, unit.Id, request.StartDate, now);
            dbContext.FuncionariosUnidadesAtuacao.Add(relationship);
            var data = new { relationshipGuid = relationship.Guid, unitGuid = unit.Guid, request.StartDate };
            AddAuditAndOutbox(
                "FuncionarioUnidadeAtuacaoAdicionada", "UNIDADE_ATUACAO_ADICIONADA",
                employee.Guid, context, null, data, now);
            return relationship.Guid;
        }, cancellationToken);

        return relationshipGuid.HasValue
            ? await GetActingUnitAsync(relationshipGuid.Value, cancellationToken)
            : null;
    }

    public Task<bool?> EndActingUnitAsync(
        Guid employeeGuid,
        Guid relationshipGuid,
        DateOnly endDate,
        EmployeeOperationContext context,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(async () =>
        {
            var relationship = await dbContext.FuncionariosUnidadesAtuacao
                .Include(x => x.Funcionario)
                .Include(x => x.UnidadeHospitalar)
                .SingleOrDefaultAsync(x =>
                    x.Guid == relationshipGuid && x.Funcionario.Guid == employeeGuid,
                    cancellationToken);
            if (relationship is null)
            {
                return (bool?)null;
            }

            if (relationship.Ativo && await dbContext.FuncionariosSetores.AnyAsync(x =>
                    x.FuncionarioId == relationship.FuncionarioId
                    && x.Setor.UnidadeHospitalarId == relationship.UnidadeHospitalarId
                    && x.Ativo,
                    cancellationToken))
            {
                throw new DomainException(
                    "Encerre os vínculos ativos com setores da unidade antes de encerrar a atuação.");
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
                "FuncionarioUnidadeAtuacaoEncerrada", "UNIDADE_ATUACAO_ENCERRADA",
                relationship.Funcionario.Guid, context, null, data, now);
            return true;
        }, cancellationToken);

    public async Task<EmployeeSectorResponse?> AddSectorAsync(
        Guid employeeGuid,
        AddEmployeeSectorRequest request,
        EmployeeOperationContext context,
        CancellationToken cancellationToken)
    {
        var relationshipGuid = await ExecuteMutationAsync(async () =>
        {
            var employee = await dbContext.Funcionarios
                .Include(x => x.UnidadeContratacao)
                .SingleOrDefaultAsync(x => x.Guid == employeeGuid, cancellationToken);
            if (employee is null)
            {
                return (Guid?)null;
            }

            EnsureEmployeeIsActive(employee);
            var sector = await ResolveSectorAsync(request.SectorGuid, cancellationToken);
            EnsureSameOrganization(employee.UnidadeContratacao.OrganizacaoId, sector.OrganizationId);
            var actingUnit = await dbContext.FuncionariosUnidadesAtuacao
                .Where(x =>
                    x.FuncionarioId == employee.Id && x.UnidadeHospitalarId == sector.UnitId && x.Ativo)
                .Select(x => new { x.DataInicio })
                .SingleOrDefaultAsync(cancellationToken);
            if (actingUnit is null)
            {
                throw new DomainException("O funcionário precisa possuir vínculo ativo com a unidade do setor.");
            }

            if (request.StartDate < employee.DataAdmissao || request.StartDate < actingUnit.DataInicio)
            {
                throw new DomainException(
                    "A atuação no setor não pode começar antes da admissão ou da atuação na unidade.");
            }

            if (await dbContext.FuncionariosSetores.AnyAsync(x =>
                    x.FuncionarioId == employee.Id && x.SetorId == sector.Id && x.Ativo,
                    cancellationToken))
            {
                throw new DomainException("O funcionário já possui vínculo ativo com o setor informado.");
            }

            if (await dbContext.FuncionariosSetores.AnyAsync(x =>
                    x.FuncionarioId == employee.Id
                    && x.SetorId == sector.Id
                    && x.DataFim.HasValue
                    && x.DataFim.Value >= request.StartDate,
                    cancellationToken))
            {
                throw new DomainException("O novo vínculo com o setor não pode sobrepor um período anterior.");
            }

            var now = timeProvider.GetUtcNow();
            var relationship = new FuncionarioSetor(
                Guid.NewGuid(), employee.Id, sector.Id, request.StartDate, now);
            dbContext.FuncionariosSetores.Add(relationship);
            var data = new { relationshipGuid = relationship.Guid, sectorGuid = sector.Guid, request.StartDate };
            AddAuditAndOutbox(
                "FuncionarioSetorAdicionado", "SETOR_ADICIONADO",
                employee.Guid, context, null, data, now);
            return relationship.Guid;
        }, cancellationToken);

        return relationshipGuid.HasValue
            ? await GetSectorAsync(relationshipGuid.Value, cancellationToken)
            : null;
    }

    public Task<bool?> EndSectorAsync(
        Guid employeeGuid,
        Guid relationshipGuid,
        DateOnly endDate,
        EmployeeOperationContext context,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(async () =>
        {
            var relationship = await dbContext.FuncionariosSetores
                .Include(x => x.Funcionario)
                .Include(x => x.Setor)
                .SingleOrDefaultAsync(x =>
                    x.Guid == relationshipGuid && x.Funcionario.Guid == employeeGuid,
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
                sectorGuid = relationship.Setor.Guid,
                endDate
            };
            AddAuditAndOutbox(
                "FuncionarioSetorEncerrado", "SETOR_ENCERRADO",
                relationship.Funcionario.Guid, context, null, data, now);
            return true;
        }, cancellationToken);

    private async Task<EmployeeResponse?> BuildResponseAsync(Guid employeeGuid, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Funcionarios.AsNoTracking()
            .Where(x => x.Guid == employeeGuid)
            .Select(x => new
            {
                x.Id,
                x.Guid,
                x.Matricula,
                x.Nome,
                x.Email,
                x.Telefone,
                x.DataAdmissao,
                x.Ativo,
                Profession = new EmployeeReferenceResponse(x.Profissao.Guid, x.Profissao.Nome),
                Position = new EmployeeReferenceResponse(x.Cargo.Guid, x.Cargo.Nome),
                Level = new EmployeeLevelResponse(x.Nivel.Guid, x.Nivel.Codigo, x.Nivel.Nome),
                HiringUnit = new EmployeeReferenceResponse(x.UnidadeContratacao.Guid, x.UnidadeContratacao.Nome),
                x.DataCriacao,
                x.DataAtualizacao
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return null;
        }

        var actingUnits = await dbContext.FuncionariosUnidadesAtuacao.AsNoTracking()
            .Where(x => x.FuncionarioId == employee.Id)
            .OrderByDescending(x => x.Ativo)
            .ThenByDescending(x => x.DataInicio)
            .Select(x => new EmployeeActingUnitResponse(
                x.Guid, x.UnidadeHospitalar.Guid, x.UnidadeHospitalar.Nome,
                x.DataInicio, x.DataFim, x.Ativo))
            .ToListAsync(cancellationToken);
        var sectors = await dbContext.FuncionariosSetores.AsNoTracking()
            .Where(x => x.FuncionarioId == employee.Id)
            .OrderByDescending(x => x.Ativo)
            .ThenByDescending(x => x.DataInicio)
            .Select(x => new EmployeeSectorResponse(
                x.Guid, x.Setor.Guid, x.Setor.Nome,
                x.Setor.UnidadeHospitalar.Guid, x.Setor.UnidadeHospitalar.Nome,
                x.DataInicio, x.DataFim, x.Ativo))
            .ToListAsync(cancellationToken);

        return new EmployeeResponse(
            employee.Guid, employee.Matricula, employee.Nome, employee.Email, employee.Telefone,
            employee.DataAdmissao, employee.Ativo, employee.Profession, employee.Position,
            employee.Level, employee.HiringUnit, actingUnits, sectors,
            employee.DataCriacao, employee.DataAtualizacao);
    }

    private async Task<EmployeeResponse> GetRequiredAsync(Guid employeeGuid, CancellationToken cancellationToken) =>
        await BuildResponseAsync(employeeGuid, cancellationToken)
        ?? throw new InvalidOperationException("O funcionário persistido não pôde ser recuperado.");

    private async Task<EmployeeActingUnitResponse> GetActingUnitAsync(Guid relationshipGuid, CancellationToken cancellationToken) =>
        await dbContext.FuncionariosUnidadesAtuacao.AsNoTracking()
            .Where(x => x.Guid == relationshipGuid)
            .Select(x => new EmployeeActingUnitResponse(
                x.Guid, x.UnidadeHospitalar.Guid, x.UnidadeHospitalar.Nome,
                x.DataInicio, x.DataFim, x.Ativo))
            .SingleAsync(cancellationToken);

    private async Task<EmployeeSectorResponse> GetSectorAsync(Guid relationshipGuid, CancellationToken cancellationToken) =>
        await dbContext.FuncionariosSetores.AsNoTracking()
            .Where(x => x.Guid == relationshipGuid)
            .Select(x => new EmployeeSectorResponse(
                x.Guid, x.Setor.Guid, x.Setor.Nome,
                x.Setor.UnidadeHospitalar.Guid, x.Setor.UnidadeHospitalar.Nome,
                x.DataInicio, x.DataFim, x.Ativo))
            .SingleAsync(cancellationToken);

    private async Task EnsureEmailIsAvailableAsync(
        string email,
        long? ignoredEmployeeId,
        CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (await dbContext.Funcionarios.AnyAsync(x =>
                x.Email == normalized && (!ignoredEmployeeId.HasValue || x.Id != ignoredEmployeeId.Value),
                cancellationToken))
        {
            throw new DomainException("Já existe um funcionário com o e-mail informado.");
        }
    }

    private async Task<ProfessionalReferences> ResolveProfessionalReferencesAsync(
        Guid professionGuid,
        Guid positionGuid,
        Guid levelGuid,
        CancellationToken cancellationToken)
    {
        var professionId = await dbContext.Profissoes
            .Where(x => x.Guid == professionGuid && x.Ativo)
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("A profissão informada não existe ou está inativa.");
        var positionId = await dbContext.Cargos
            .Where(x => x.Guid == positionGuid && x.Ativo)
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("O cargo informado não existe ou está inativo.");
        var levelId = await dbContext.NiveisProfissionais
            .Where(x => x.Guid == levelGuid && x.Ativo)
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("O nível profissional informado não existe ou está inativo.");
        return new ProfessionalReferences(professionId, positionId, levelId);
    }

    private async Task<UnitReference> ResolveUnitAsync(Guid unitGuid, CancellationToken cancellationToken) =>
        await dbContext.UnidadesHospitalares
            .Where(x => x.Guid == unitGuid && x.Ativo && x.Organizacao.Ativo)
            .Select(x => new UnitReference(x.Id, x.Guid, x.OrganizacaoId, default))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new DomainException("A unidade hospitalar informada não existe ou está inativa.");

    private async Task<IReadOnlyCollection<UnitReference>> ResolveActingUnitsAsync(
        IReadOnlyCollection<CreateEmployeeActingUnitRequest> requests,
        long organizationId,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        if (requests.Select(x => x.UnitGuid).Distinct().Count() != requests.Count)
        {
            throw new DomainException("A lista de unidades de atuação contém itens duplicados.");
        }

        var dates = requests.ToDictionary(x => x.UnitGuid, x => x.StartDate);
        var persistedUnits = await dbContext.UnidadesHospitalares
            .Where(x => dates.Keys.Contains(x.Guid) && x.Ativo && x.Organizacao.Ativo)
            .Select(x => new { x.Id, x.Guid, x.OrganizacaoId })
            .ToListAsync(cancellationToken);
        if (persistedUnits.Count != requests.Count)
        {
            throw new DomainException("Uma ou mais unidades de atuação não existem ou estão inativas.");
        }

        var units = persistedUnits
            .Select(x => new UnitReference(x.Id, x.Guid, x.OrganizacaoId, dates[x.Guid]))
            .ToList();

        if (units.Any(x => x.OrganizationId != organizationId))
        {
            throw new DomainException("A unidade de atuação deve pertencer à organização da unidade de contratação.");
        }

        return units;
    }

    private async Task<IReadOnlyCollection<SectorReference>> ResolveSectorsAsync(
        IReadOnlyCollection<CreateEmployeeSectorRequest> requests,
        long organizationId,
        IReadOnlySet<long> actingUnitIds,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        if (requests.Select(x => x.SectorGuid).Distinct().Count() != requests.Count)
        {
            throw new DomainException("A lista de setores contém itens duplicados.");
        }

        var dates = requests.ToDictionary(x => x.SectorGuid, x => x.StartDate);
        var persistedSectors = await dbContext.Setores
            .Where(x => dates.Keys.Contains(x.Guid) && x.Ativo && x.UnidadeHospitalar.Ativo)
            .Select(x => new SectorReference(
                x.Id, x.Guid, x.UnidadeHospitalarId, x.UnidadeHospitalar.OrganizacaoId, default))
            .ToListAsync(cancellationToken);
        if (persistedSectors.Count != requests.Count)
        {
            throw new DomainException("Um ou mais setores não existem ou estão inativos.");
        }

        var sectors = persistedSectors
            .Select(x => x with { StartDate = dates[x.Guid] })
            .ToList();

        if (sectors.Any(x => x.OrganizationId != organizationId))
        {
            throw new DomainException("O setor deve pertencer à organização da unidade de contratação.");
        }

        if (sectors.Any(x => !actingUnitIds.Contains(x.UnitId)))
        {
            throw new DomainException("O funcionário precisa possuir vínculo de atuação com a unidade de cada setor.");
        }

        return sectors;
    }

    private async Task<SectorReference> ResolveSectorAsync(Guid sectorGuid, CancellationToken cancellationToken) =>
        await dbContext.Setores
            .Where(x => x.Guid == sectorGuid && x.Ativo && x.UnidadeHospitalar.Ativo)
            .Select(x => new SectorReference(
                x.Id, x.Guid, x.UnidadeHospitalarId, x.UnidadeHospitalar.OrganizacaoId, default))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new DomainException("O setor informado não existe ou está inativo.");

    private static void EnsureSameOrganization(long expectedOrganizationId, long actualOrganizationId)
    {
        if (expectedOrganizationId != actualOrganizationId)
        {
            throw new DomainException("O vínculo deve permanecer dentro da organização da unidade de contratação.");
        }
    }

    private static void EnsureEmployeeIsActive(Funcionario employee)
    {
        if (!employee.Ativo)
        {
            throw new DomainException("Não é possível adicionar vínculos a um funcionário inativo.");
        }
    }

    private void AddAuditAndOutbox(
        string eventType,
        string action,
        Guid employeeGuid,
        EmployeeOperationContext context,
        object? before,
        object after,
        DateTimeOffset now)
    {
        var previousJson = before is null ? null : JsonSerializer.Serialize(before);
        var newJson = JsonSerializer.Serialize(after);
        dbContext.AuditLogs.Add(new AuditLog(
            Guid.NewGuid(), "Funcionario", employeeGuid, action, context.ActorUserGuid,
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
                if (Interlocked.Increment(ref attempt) > 1)
                {
                    dbContext.ChangeTracker.Clear();
                }

                await using var transaction = await BeginTransactionAsync(cancellationToken);
                var result = await mutation();
                await dbContext.SaveChangesAsync(cancellationToken);
                await CommitAsync(transaction, cancellationToken);
                return result;
            });
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new EmployeePersistenceConflictException(
                "A operação conflitou com outra alteração concorrente.", exception);
        }
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    private sealed record ProfessionalReferences(long ProfessionId, long PositionId, long LevelId);
    private sealed record UnitReference(long Id, Guid Guid, long OrganizationId, DateOnly StartDate);
    private sealed record SectorReference(long Id, Guid Guid, long UnitId, long OrganizationId, DateOnly StartDate);
}

public sealed class EmployeePersistenceConflictException(string message, Exception innerException)
    : Exception(message, innerException);
