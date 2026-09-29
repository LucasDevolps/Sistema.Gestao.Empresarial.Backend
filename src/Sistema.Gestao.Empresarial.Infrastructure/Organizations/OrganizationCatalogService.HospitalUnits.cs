using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;

namespace Sistema.Gestao.Empresarial.Infrastructure.Organizations;

public sealed partial class OrganizationCatalogService
{
    private static readonly Expression<Func<UnidadeHospitalar, HospitalUnitSummaryResponse>> HospitalSummary = x =>
        new HospitalUnitSummaryResponse(x.Guid, x.Nome, x.Ativo,
            new OrganizationReferenceResponse(x.Organizacao.Guid, x.Organizacao.Nome), x.DataCriacao, x.DataAtualizacao,
            x.RazaoSocial, x.Cnpj, x.Cnes, x.Cidade, x.Uf);

    private static readonly Expression<Func<UnidadeHospitalar, HospitalUnitResponse>> HospitalDetail = x =>
        new HospitalUnitResponse(x.Guid, x.Nome, x.Ativo,
            new OrganizationReferenceResponse(x.Organizacao.Guid, x.Organizacao.Nome), x.DataCriacao, x.DataAtualizacao)
        {
            LegalName = x.RazaoSocial,
            Cnpj = x.Cnpj,
            HasOwnCnpj = x.PossuiCnpjProprio,
            Cnes = x.Cnes,
            UnitType = x.Tipo,
            Nature = x.Natureza,
            InternalCode = x.CodigoInterno,
            Acronym = x.Sigla,
            ActivityStartDate = x.InicioAtividades,
            RegistrationStatus = x.SituacaoCadastral,
            OpeningDate = x.DataAbertura,
            LegalNature = x.NaturezaJuridica,
            PrimaryCnae = x.CnaePrincipal,
            SecondaryCnaes = x.CnaesSecundarios,
            StateRegistration = x.InscricaoEstadual,
            MunicipalRegistration = x.InscricaoMunicipal,
            PostalCode = x.Cep,
            Street = x.Logradouro,
            Number = x.Numero,
            Complement = x.Complemento,
            District = x.Bairro,
            City = x.Cidade,
            State = x.Uf,
            IbgeCode = x.CodigoIbge,
            Region = x.Regiao,
            AreaCode = x.Ddd,
            AddressReference = x.ReferenciaEndereco,
            Phone = x.TelefonePrincipal,
            SecondaryPhone = x.TelefoneSecundario,
            Whatsapp = x.Whatsapp,
            Email = x.EmailInstitucional,
            AdministrativeEmail = x.EmailAdministrativo,
            Website = x.Site,
            Extension = x.Ramal,
            AdministrativeResponsibleName = x.ResponsavelAdministrativoNome,
            AdministrativeResponsibleRole = x.ResponsavelAdministrativoCargo,
            AdministrativeResponsibleEmail = x.ResponsavelAdministrativoEmail,
            AdministrativeResponsiblePhone = x.ResponsavelAdministrativoTelefone,
            TechnicalResponsibleName = x.ResponsavelTecnicoNome,
            TechnicalResponsibleProfession = x.ResponsavelTecnicoProfissao,
            TechnicalResponsibleCouncil = x.ResponsavelTecnicoConselho,
            TechnicalResponsibleCouncilNumber = x.ResponsavelTecnicoRegistro,
            TechnicalResponsibleCouncilState = x.ResponsavelTecnicoUf,
            TechnicalResponsibleEmail = x.ResponsavelTecnicoEmail,
            TechnicalResponsiblePhone = x.ResponsavelTecnicoTelefone,
            ClinicalDirectorName = x.DiretorClinicoNome,
            ClinicalDirectorCrm = x.DiretorClinicoCrm,
            ClinicalDirectorCrmState = x.DiretorClinicoUf,
            ClinicalDirectorEmail = x.DiretorClinicoEmail,
            ClinicalDirectorPhone = x.DiretorClinicoTelefone,
            SanitaryPermit = x.AlvaraSanitario,
            SanitaryPermitExpiry = x.ValidadeAlvaraSanitario,
            OperatingLicense = x.LicencaFuncionamento,
            OperatingLicenseExpiry = x.ValidadeLicencaFuncionamento,
            RegulatoryNotes = x.ObservacoesRegulatorias,
            Open24Hours = x.Atendimento24h,
            HasEmergencyRoom = x.ProntoSocorro,
            HasInpatientCare = x.Internacao,
            HasIcu = x.Uti,
            TotalBeds = x.TotalLeitos,
            IcuBeds = x.LeitosUti,
            HasSurgicalCenter = x.CentroCirurgico,
            HasMaternity = x.Maternidade,
            HasOutpatientCare = x.AtendimentoAmbulatorial,
            Notes = x.ObservacoesGerais
        };

    public async Task<HospitalUnitPageResponse> ListHospitalUnitsAsync(Guid actorUserGuid, OrganizationCatalogListQuery query, CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        var units = dbContext.UnidadesHospitalares.AsNoTracking().Where(x => x.OrganizacaoId == organizationId);
        if (!string.IsNullOrWhiteSpace(query.Search)) units = units.Where(x => x.Nome.Contains(query.Search.Trim()));
        if (!string.IsNullOrWhiteSpace(query.LegalName)) units = units.Where(x => x.RazaoSocial != null && x.RazaoSocial.Contains(query.LegalName.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Cnpj))
        {
            var cnpj = CadastroBrasileiro.SemMascaraCnpj(query.Cnpj);
            units = units.Where(x => x.Cnpj == cnpj);
        }
        if (!string.IsNullOrWhiteSpace(query.Cnes)) units = units.Where(x => x.Cnes == query.Cnes.Trim());
        if (!string.IsNullOrWhiteSpace(query.City)) units = units.Where(x => x.Cidade != null && x.Cidade.Contains(query.City.Trim()));
        if (!string.IsNullOrWhiteSpace(query.State)) units = units.Where(x => x.Uf == query.State.Trim().ToUpper());
        if (query.OrganizationGuid.HasValue) units = units.Where(x => x.Organizacao.Guid == query.OrganizationGuid.Value);
        if (query.Active.HasValue) units = units.Where(x => x.Ativo == query.Active.Value);
        var total = await units.CountAsync(cancellationToken);
        var items = await units.OrderBy(x => x.Nome).ThenBy(x => x.Guid)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(HospitalSummary).ToListAsync(cancellationToken);
        return new HospitalUnitPageResponse(items, query.Page, query.PageSize, total);
    }

    public async Task<HospitalUnitResponse?> GetHospitalUnitAsync(Guid actorUserGuid, Guid unitGuid, CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        return await dbContext.UnidadesHospitalares.AsNoTracking()
            .Where(x => x.Guid == unitGuid && x.OrganizacaoId == organizationId)
            .Select(HospitalDetail).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<HospitalUnitResponse> CreateHospitalUnitAsync(HospitalUnitRegistrationRequest request, HospitalUnitOperationContext context, CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(context.ActorUserGuid, cancellationToken);
        var guid = await ExecuteMutationAsync(async () =>
        {
            await EnsureHospitalOrganizationAsync(organizationId, request.OrganizationGuid, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var unit = new UnidadeHospitalar(Guid.NewGuid(), organizationId, request.ToDomain(), now);
            await EnsureHospitalKeysAsync(unit, cancellationToken);
            dbContext.UnidadesHospitalares.Add(unit);
            AuditHospital(unit, "CRIADA", "UnidadeHospitalarCriada", context, null, now);
            return unit.Guid;
        }, cancellationToken);
        return (await GetHospitalUnitAsync(context.ActorUserGuid, guid, cancellationToken))!;
    }

    public async Task<HospitalUnitResponse?> UpdateHospitalUnitAsync(Guid unitGuid, HospitalUnitRegistrationRequest request, HospitalUnitOperationContext context, CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(context.ActorUserGuid, cancellationToken);
        var found = await ExecuteMutationAsync(async () =>
        {
            var unit = await dbContext.UnidadesHospitalares.SingleOrDefaultAsync(x => x.Guid == unitGuid && x.OrganizacaoId == organizationId, cancellationToken);
            if (unit is null) return false;
            await EnsureHospitalOrganizationAsync(organizationId, request.OrganizationGuid, cancellationToken);
            var before = HospitalSnapshot(unit);
            var now = timeProvider.GetUtcNow();
            unit.AtualizarCadastro(request.ToDomain(), now);
            await EnsureHospitalKeysAsync(unit, cancellationToken);
            AuditHospital(unit, "ATUALIZADA", "UnidadeHospitalarAtualizada", context, before, now);
            return true;
        }, cancellationToken);
        return found ? await GetHospitalUnitAsync(context.ActorUserGuid, unitGuid, cancellationToken) : null;
    }

    public async Task<HospitalUnitResponse?> ChangeHospitalUnitStatusAsync(Guid unitGuid, bool active, HospitalUnitOperationContext context, CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(context.ActorUserGuid, cancellationToken);
        var found = await ExecuteMutationAsync(async () =>
        {
            var unit = await dbContext.UnidadesHospitalares.SingleOrDefaultAsync(x => x.Guid == unitGuid && x.OrganizacaoId == organizationId, cancellationToken);
            if (unit is null) return false;
            if (unit.Ativo == active) return true;
            var before = HospitalSnapshot(unit);
            var now = timeProvider.GetUtcNow();
            if (active) unit.Reativar(now); else unit.Inativar(now);
            AuditHospital(unit, active ? "REATIVADA" : "INATIVADA", active ? "UnidadeHospitalarReativada" : "UnidadeHospitalarInativada", context, before, now);
            return true;
        }, cancellationToken);
        return found ? await GetHospitalUnitAsync(context.ActorUserGuid, unitGuid, cancellationToken) : null;
    }

    private async Task EnsureHospitalOrganizationAsync(long organizationId, Guid? requestedGuid, CancellationToken cancellationToken)
    {
        if (!await dbContext.Organizacoes.AnyAsync(x => x.Id == organizationId && x.Ativo
            && (!requestedGuid.HasValue || x.Guid == requestedGuid.Value), cancellationToken))
            throw new OrganizationAccessDeniedException();
    }

    private async Task EnsureHospitalKeysAsync(UnidadeHospitalar unit, CancellationToken cancellationToken)
    {
        // Chaves globais continuam reservadas mesmo em unidades inativas/excluídas.
        var others = dbContext.UnidadesHospitalares.IgnoreQueryFilters().Where(x => x.Guid != unit.Guid);
        if (unit.Cnpj is not null && await others.AnyAsync(x => x.Cnpj == unit.Cnpj, cancellationToken))
            throw new DuplicateBusinessKeyException("Já existe uma unidade com este CNPJ.", "cnpj");
        if (unit.Cnes is not null && await others.AnyAsync(x => x.Cnes == unit.Cnes, cancellationToken))
            throw new DuplicateBusinessKeyException("Já existe uma unidade com este CNES.", "cnes");
        if (unit.CodigoInterno is not null && await others.AnyAsync(x => x.OrganizacaoId == unit.OrganizacaoId && x.CodigoInterno == unit.CodigoInterno, cancellationToken))
            throw new DuplicateBusinessKeyException("Já existe uma unidade com este código interno nesta organização.", "internalCode");
    }

    private void AuditHospital(UnidadeHospitalar unit, string action, string eventType, HospitalUnitOperationContext context, object? before, DateTimeOffset now) =>
        AddAuditAndOutbox(eventType, "UnidadeHospitalar", action, unit.Guid,
            new SectorOperationContext(context.ActorUserGuid, context.CorrelationId, context.TraceId, context.IpAddress),
            before, HospitalSnapshot(unit), now);

    // Auditoria institucional mínima: não copia contatos ou dados pessoais dos responsáveis.
    private static object HospitalSnapshot(UnidadeHospitalar unit) => new
    {
        unit.Guid, unit.Nome, unit.Ativo, unit.CodigoInterno, unit.Tipo, unit.Natureza, unit.Cidade, unit.Uf
    };

    public async Task<IReadOnlyCollection<HospitalUnitSummaryResponse>> FindHospitalUnitDuplicatesAsync(Guid actorUserGuid, HospitalUnitDuplicateQuery query, CancellationToken cancellationToken)
    {
        var organizationId = await GetActorOrganizationIdAsync(actorUserGuid, cancellationToken);
        var cep = CadastroBrasileiro.NormalizarCep(query.PostalCode);
        var number = query.Number.Trim();
        // A triagem é limitada ao mesmo endereço, com teto de 100 candidatos e 20 alertas.
        var candidates = await dbContext.UnidadesHospitalares.AsNoTracking()
            .Where(x => x.OrganizacaoId == organizationId && x.Cep == cep && x.Numero == number
                && (!query.ExcludeGuid.HasValue || x.Guid != query.ExcludeGuid.Value))
            .OrderBy(x => x.Guid).Take(100).Select(HospitalSummary).ToListAsync(cancellationToken);
        return candidates.Where(x => SimilarHospitalName(x.Name, query.Name)
            || SimilarHospitalName(x.LegalName, query.LegalName)).Take(20).ToArray();
    }

    private static bool SimilarHospitalName(string? left, string? right)
    {
        static HashSet<string> Tokens(string value) => new(string.Concat(value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' '))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        var a = Tokens(left); var b = Tokens(right);
        return a.Count > 0 && (double)a.Intersect(b).Count() / a.Union(b).Count() >= 0.8;
    }
}
