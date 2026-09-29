using Sistema.Gestao.Empresarial.Domain.Organizacoes;

namespace Sistema.Gestao.Empresarial.Application.Organizations;

/// <summary>Cadastro completo para criação e substituição. Name representa nome fantasia.</summary>
public sealed record HospitalUnitRegistrationRequest
{
    public string Name { get; init; } = string.Empty;
    public string? LegalName { get; init; }
    public string? Cnpj { get; init; }
    public bool HasOwnCnpj { get; init; }
    public string? Cnes { get; init; }
    public TipoUnidadeHospitalar? UnitType { get; init; }
    public NaturezaUnidadeHospitalar? Nature { get; init; }
    public string? InternalCode { get; init; }
    public string? Acronym { get; init; }
    public DateOnly? ActivityStartDate { get; init; }
    public string? RegistrationStatus { get; init; }
    public DateOnly? OpeningDate { get; init; }
    public string? LegalNature { get; init; }
    public string? PrimaryCnae { get; init; }
    public string? SecondaryCnaes { get; init; }
    public string? StateRegistration { get; init; }
    public string? MunicipalRegistration { get; init; }
    public string? PostalCode { get; init; }
    public string? Street { get; init; }
    public string? Number { get; init; }
    public string? Complement { get; init; }
    public string? District { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? IbgeCode { get; init; }
    public string? Region { get; init; }
    public string? AreaCode { get; init; }
    public string? AddressReference { get; init; }
    public string? Phone { get; init; }
    public string? SecondaryPhone { get; init; }
    public string? Whatsapp { get; init; }
    public string? Email { get; init; }
    public string? AdministrativeEmail { get; init; }
    public string? Website { get; init; }
    public string? Extension { get; init; }
    public string? AdministrativeResponsibleName { get; init; }
    public string? AdministrativeResponsibleRole { get; init; }
    public string? AdministrativeResponsibleEmail { get; init; }
    public string? AdministrativeResponsiblePhone { get; init; }
    public string? TechnicalResponsibleName { get; init; }
    public string? TechnicalResponsibleProfession { get; init; }
    public string? TechnicalResponsibleCouncil { get; init; }
    public string? TechnicalResponsibleCouncilNumber { get; init; }
    public string? TechnicalResponsibleCouncilState { get; init; }
    public string? TechnicalResponsibleEmail { get; init; }
    public string? TechnicalResponsiblePhone { get; init; }
    public string? ClinicalDirectorName { get; init; }
    public string? ClinicalDirectorCrm { get; init; }
    public string? ClinicalDirectorCrmState { get; init; }
    public string? ClinicalDirectorEmail { get; init; }
    public string? ClinicalDirectorPhone { get; init; }
    public string? SanitaryPermit { get; init; }
    public DateOnly? SanitaryPermitExpiry { get; init; }
    public string? OperatingLicense { get; init; }
    public DateOnly? OperatingLicenseExpiry { get; init; }
    public string? RegulatoryNotes { get; init; }
    public bool? Open24Hours { get; init; }
    public bool? HasEmergencyRoom { get; init; }
    public bool? HasInpatientCare { get; init; }
    public bool? HasIcu { get; init; }
    public int? TotalBeds { get; init; }
    public int? IcuBeds { get; init; }
    public bool? HasSurgicalCenter { get; init; }
    public bool? HasMaternity { get; init; }
    public bool? HasOutpatientCare { get; init; }
    public string? Notes { get; init; }
    /// <summary>Opcional. Quando informado deve ser a organização do ator; não permite transferência.</summary>
    public Guid? OrganizationGuid { get; init; }

    public CadastroUnidadeHospitalar ToDomain() => new()
    {
        Nome = Name,
        RazaoSocial = LegalName,
        Cnpj = Cnpj,
        PossuiCnpjProprio = HasOwnCnpj,
        Cnes = Cnes,
        Tipo = UnitType,
        Natureza = Nature,
        CodigoInterno = InternalCode,
        Sigla = Acronym,
        InicioAtividades = ActivityStartDate,
        SituacaoCadastral = RegistrationStatus,
        DataAbertura = OpeningDate,
        NaturezaJuridica = LegalNature,
        CnaePrincipal = PrimaryCnae,
        CnaesSecundarios = SecondaryCnaes,
        InscricaoEstadual = StateRegistration,
        InscricaoMunicipal = MunicipalRegistration,
        Cep = PostalCode,
        Logradouro = Street,
        Numero = Number,
        Complemento = Complement,
        Bairro = District,
        Cidade = City,
        Uf = State,
        CodigoIbge = IbgeCode,
        Regiao = Region,
        Ddd = AreaCode,
        ReferenciaEndereco = AddressReference,
        TelefonePrincipal = Phone,
        TelefoneSecundario = SecondaryPhone,
        Whatsapp = Whatsapp,
        EmailInstitucional = Email,
        EmailAdministrativo = AdministrativeEmail,
        Site = Website,
        Ramal = Extension,
        ResponsavelAdministrativoNome = AdministrativeResponsibleName,
        ResponsavelAdministrativoCargo = AdministrativeResponsibleRole,
        ResponsavelAdministrativoEmail = AdministrativeResponsibleEmail,
        ResponsavelAdministrativoTelefone = AdministrativeResponsiblePhone,
        ResponsavelTecnicoNome = TechnicalResponsibleName,
        ResponsavelTecnicoProfissao = TechnicalResponsibleProfession,
        ResponsavelTecnicoConselho = TechnicalResponsibleCouncil,
        ResponsavelTecnicoRegistro = TechnicalResponsibleCouncilNumber,
        ResponsavelTecnicoUf = TechnicalResponsibleCouncilState,
        ResponsavelTecnicoEmail = TechnicalResponsibleEmail,
        ResponsavelTecnicoTelefone = TechnicalResponsiblePhone,
        DiretorClinicoNome = ClinicalDirectorName,
        DiretorClinicoCrm = ClinicalDirectorCrm,
        DiretorClinicoUf = ClinicalDirectorCrmState,
        DiretorClinicoEmail = ClinicalDirectorEmail,
        DiretorClinicoTelefone = ClinicalDirectorPhone,
        AlvaraSanitario = SanitaryPermit,
        ValidadeAlvaraSanitario = SanitaryPermitExpiry,
        LicencaFuncionamento = OperatingLicense,
        ValidadeLicencaFuncionamento = OperatingLicenseExpiry,
        ObservacoesRegulatorias = RegulatoryNotes,
        Atendimento24h = Open24Hours,
        ProntoSocorro = HasEmergencyRoom,
        Internacao = HasInpatientCare,
        Uti = HasIcu,
        TotalLeitos = TotalBeds,
        LeitosUti = IcuBeds,
        CentroCirurgico = HasSurgicalCenter,
        Maternidade = HasMaternity,
        AtendimentoAmbulatorial = HasOutpatientCare,
        ObservacoesGerais = Notes
    };
}

/// <summary>Alteração de estado sem exclusão de vínculos.</summary>
public sealed record ChangeHospitalUnitStatusRequest(bool Active);

/// <summary>Contexto de auditoria originado da sessão, nunca do payload.</summary>
public sealed record HospitalUnitOperationContext(Guid ActorUserGuid, Guid CorrelationId, string TraceId, string? IpAddress);

/// <summary>Candidatos de similaridade. A resposta é informativa e nunca impede gravação.</summary>
public sealed record HospitalUnitDuplicateQuery(string Name, string? LegalName, string PostalCode, string Number, Guid? ExcludeGuid = null);

/// <summary>Dados resumidos para pesquisa, preservando as propriedades do contrato anterior.</summary>
public sealed record HospitalUnitSummaryResponse(
    Guid Guid, string Name, bool Active, OrganizationReferenceResponse Organization,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    string? LegalName, string? Cnpj, string? Cnes, string? City, string? State);
