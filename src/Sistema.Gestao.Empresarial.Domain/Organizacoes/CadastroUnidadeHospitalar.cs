using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

/// <summary>Dados institucionais, sem identidade ou relacionamentos mutáveis.</summary>
public sealed record CadastroUnidadeHospitalar
{
    public string Nome { get; init; } = string.Empty;
    public string? RazaoSocial { get; init; }
    public string? Cnpj { get; init; }
    public bool PossuiCnpjProprio { get; init; }
    public string? Cnes { get; init; }
    public TipoUnidadeHospitalar? Tipo { get; init; }
    public NaturezaUnidadeHospitalar? Natureza { get; init; }
    public string? CodigoInterno { get; init; }
    public string? Sigla { get; init; }
    public DateOnly? InicioAtividades { get; init; }
    public string? SituacaoCadastral { get; init; }
    public DateOnly? DataAbertura { get; init; }
    public string? NaturezaJuridica { get; init; }
    public string? CnaePrincipal { get; init; }
    public string? CnaesSecundarios { get; init; }
    public string? InscricaoEstadual { get; init; }
    public string? InscricaoMunicipal { get; init; }
    public string? Cep { get; init; }
    public string? Logradouro { get; init; }
    public string? Numero { get; init; }
    public string? Complemento { get; init; }
    public string? Bairro { get; init; }
    public string? Cidade { get; init; }
    public string? Uf { get; init; }
    public string? CodigoIbge { get; init; }
    public string? Regiao { get; init; }
    public string? Ddd { get; init; }
    public string? ReferenciaEndereco { get; init; }
    public string? TelefonePrincipal { get; init; }
    public string? TelefoneSecundario { get; init; }
    public string? Whatsapp { get; init; }
    public string? EmailInstitucional { get; init; }
    public string? EmailAdministrativo { get; init; }
    public string? Site { get; init; }
    public string? Ramal { get; init; }
    public string? ResponsavelAdministrativoNome { get; init; }
    public string? ResponsavelAdministrativoCargo { get; init; }
    public string? ResponsavelAdministrativoEmail { get; init; }
    public string? ResponsavelAdministrativoTelefone { get; init; }
    public string? ResponsavelTecnicoNome { get; init; }
    public string? ResponsavelTecnicoProfissao { get; init; }
    public string? ResponsavelTecnicoConselho { get; init; }
    public string? ResponsavelTecnicoRegistro { get; init; }
    public string? ResponsavelTecnicoUf { get; init; }
    public string? ResponsavelTecnicoEmail { get; init; }
    public string? ResponsavelTecnicoTelefone { get; init; }
    public string? DiretorClinicoNome { get; init; }
    public string? DiretorClinicoCrm { get; init; }
    public string? DiretorClinicoUf { get; init; }
    public string? DiretorClinicoEmail { get; init; }
    public string? DiretorClinicoTelefone { get; init; }
    public string? AlvaraSanitario { get; init; }
    public DateOnly? ValidadeAlvaraSanitario { get; init; }
    public string? LicencaFuncionamento { get; init; }
    public DateOnly? ValidadeLicencaFuncionamento { get; init; }
    public string? ObservacoesRegulatorias { get; init; }
    public bool? Atendimento24h { get; init; }
    public bool? ProntoSocorro { get; init; }
    public bool? Internacao { get; init; }
    public bool? Uti { get; init; }
    public int? TotalLeitos { get; init; }
    public int? LeitosUti { get; init; }
    public bool? CentroCirurgico { get; init; }
    public bool? Maternidade { get; init; }
    public bool? AtendimentoAmbulatorial { get; init; }
    public string? ObservacoesGerais { get; init; }

    public CadastroUnidadeHospitalar NormalizarEValidar()
    {
        var data = this with
        {
            Nome = Guard.TextoObrigatorio(Nome, nameof(Nome), 200),
            RazaoSocial = Guard.TextoOpcional(RazaoSocial, nameof(RazaoSocial), 200),
            Cnpj = CadastroBrasileiro.NormalizarCnpj(Cnpj),
            Cnes = Guard.TextoOpcional(Cnes, nameof(Cnes), 7),
            CodigoInterno = Guard.TextoOpcional(CodigoInterno, nameof(CodigoInterno), 50)?.ToUpperInvariant(),
            Sigla = Guard.TextoOpcional(Sigla, nameof(Sigla), 20)?.ToUpperInvariant(),
            SituacaoCadastral = Guard.TextoOpcional(SituacaoCadastral, nameof(SituacaoCadastral), 100),
            NaturezaJuridica = Guard.TextoOpcional(NaturezaJuridica, nameof(NaturezaJuridica), 200),
            CnaePrincipal = Guard.TextoOpcional(CnaePrincipal, nameof(CnaePrincipal), 200),
            CnaesSecundarios = Guard.TextoOpcional(CnaesSecundarios, nameof(CnaesSecundarios), 2000),
            InscricaoEstadual = Guard.TextoOpcional(InscricaoEstadual, nameof(InscricaoEstadual), 50),
            InscricaoMunicipal = Guard.TextoOpcional(InscricaoMunicipal, nameof(InscricaoMunicipal), 50),
            Cep = CadastroBrasileiro.NormalizarCep(Cep),
            Logradouro = Guard.TextoObrigatorio(Logradouro, nameof(Logradouro), 200),
            Numero = Guard.TextoObrigatorio(Numero, nameof(Numero), 30),
            Complemento = Guard.TextoOpcional(Complemento, nameof(Complemento), 150),
            Bairro = Guard.TextoObrigatorio(Bairro, nameof(Bairro), 100),
            Cidade = Guard.TextoObrigatorio(Cidade, nameof(Cidade), 100),
            Uf = Guard.TextoObrigatorio(Uf, nameof(Uf), 2).ToUpperInvariant(),
            CodigoIbge = Guard.TextoOpcional(CodigoIbge, nameof(CodigoIbge), 7),
            Regiao = Guard.TextoOpcional(Regiao, nameof(Regiao), 30),
            Ddd = Guard.TextoOpcional(Ddd, nameof(Ddd), 2),
            ReferenciaEndereco = Guard.TextoOpcional(ReferenciaEndereco, nameof(ReferenciaEndereco), 300),
            TelefonePrincipal = Guard.TextoOpcional(TelefonePrincipal, nameof(TelefonePrincipal), 30),
            TelefoneSecundario = Guard.TextoOpcional(TelefoneSecundario, nameof(TelefoneSecundario), 30),
            Whatsapp = Guard.TextoOpcional(Whatsapp, nameof(Whatsapp), 30),
            EmailInstitucional = Guard.TextoOpcional(EmailInstitucional, nameof(EmailInstitucional), 254)?.ToLowerInvariant(),
            EmailAdministrativo = Guard.TextoOpcional(EmailAdministrativo, nameof(EmailAdministrativo), 254)?.ToLowerInvariant(),
            Site = Guard.TextoOpcional(Site, nameof(Site), 500),
            Ramal = Guard.TextoOpcional(Ramal, nameof(Ramal), 30),
            ResponsavelAdministrativoNome = Guard.TextoOpcional(ResponsavelAdministrativoNome, nameof(ResponsavelAdministrativoNome), 200),
            ResponsavelAdministrativoCargo = Guard.TextoOpcional(ResponsavelAdministrativoCargo, nameof(ResponsavelAdministrativoCargo), 150),
            ResponsavelAdministrativoEmail = Guard.TextoOpcional(ResponsavelAdministrativoEmail, nameof(ResponsavelAdministrativoEmail), 254)?.ToLowerInvariant(),
            ResponsavelAdministrativoTelefone = Guard.TextoOpcional(ResponsavelAdministrativoTelefone, nameof(ResponsavelAdministrativoTelefone), 30),
            ResponsavelTecnicoNome = Guard.TextoOpcional(ResponsavelTecnicoNome, nameof(ResponsavelTecnicoNome), 200),
            ResponsavelTecnicoProfissao = Guard.TextoOpcional(ResponsavelTecnicoProfissao, nameof(ResponsavelTecnicoProfissao), 150),
            ResponsavelTecnicoConselho = Guard.TextoOpcional(ResponsavelTecnicoConselho, nameof(ResponsavelTecnicoConselho), 50),
            ResponsavelTecnicoRegistro = Guard.TextoOpcional(ResponsavelTecnicoRegistro, nameof(ResponsavelTecnicoRegistro), 50),
            ResponsavelTecnicoUf = Guard.TextoOpcional(ResponsavelTecnicoUf, nameof(ResponsavelTecnicoUf), 2)?.ToUpperInvariant(),
            ResponsavelTecnicoEmail = Guard.TextoOpcional(ResponsavelTecnicoEmail, nameof(ResponsavelTecnicoEmail), 254)?.ToLowerInvariant(),
            ResponsavelTecnicoTelefone = Guard.TextoOpcional(ResponsavelTecnicoTelefone, nameof(ResponsavelTecnicoTelefone), 30),
            DiretorClinicoNome = Guard.TextoOpcional(DiretorClinicoNome, nameof(DiretorClinicoNome), 200),
            DiretorClinicoCrm = Guard.TextoOpcional(DiretorClinicoCrm, nameof(DiretorClinicoCrm), 50),
            DiretorClinicoUf = Guard.TextoOpcional(DiretorClinicoUf, nameof(DiretorClinicoUf), 2)?.ToUpperInvariant(),
            DiretorClinicoEmail = Guard.TextoOpcional(DiretorClinicoEmail, nameof(DiretorClinicoEmail), 254)?.ToLowerInvariant(),
            DiretorClinicoTelefone = Guard.TextoOpcional(DiretorClinicoTelefone, nameof(DiretorClinicoTelefone), 30),
            AlvaraSanitario = Guard.TextoOpcional(AlvaraSanitario, nameof(AlvaraSanitario), 100),
            LicencaFuncionamento = Guard.TextoOpcional(LicencaFuncionamento, nameof(LicencaFuncionamento), 100),
            ObservacoesRegulatorias = Guard.TextoOpcional(ObservacoesRegulatorias, nameof(ObservacoesRegulatorias), 2000),
            ObservacoesGerais = Guard.TextoOpcional(ObservacoesGerais, nameof(ObservacoesGerais), 2000)
        };
        if ((data.PossuiCnpjProprio && data.Cnpj is null) || (data.Cnpj is not null && data.RazaoSocial is null))
            throw new DomainException("CNPJ próprio exige CNPJ válido e razão social.");
        if (data.Tipo.HasValue && !Enum.IsDefined(data.Tipo.Value)
            || data.Natureza.HasValue && !Enum.IsDefined(data.Natureza.Value))
            throw new DomainException("Classificação de unidade inválida.");
        if (data.TotalLeitos < 0 || data.LeitosUti < 0
            || data.TotalLeitos.HasValue && data.LeitosUti > data.TotalLeitos)
            throw new DomainException("Os leitos devem ser não negativos e os leitos UTI não podem superar o total.");
        if (data.Cnes is not null && !CadastroBrasileiro.Digitos(data.Cnes, 7))
            throw new DomainException("CNES deve possuir 7 dígitos.");
        foreach (var state in new[] { data.Uf, data.ResponsavelTecnicoUf, data.DiretorClinicoUf })
            if (state is not null && !CadastroBrasileiro.UfValida(state))
                throw new DomainException("UF inválida.");
        if (data.CodigoIbge is not null && !CadastroBrasileiro.Digitos(data.CodigoIbge, 7)
            || data.Ddd is not null && !CadastroBrasileiro.Digitos(data.Ddd, 2))
            throw new DomainException("Código IBGE ou DDD inválido.");
        if (!CadastroBrasileiro.EmailValido(data.EmailInstitucional)) throw new DomainException("E-mail inválido.");
        if (!CadastroBrasileiro.EmailValido(data.EmailAdministrativo)) throw new DomainException("E-mail inválido.");
        if (!CadastroBrasileiro.EmailValido(data.ResponsavelAdministrativoEmail)) throw new DomainException("E-mail inválido.");
        if (!CadastroBrasileiro.EmailValido(data.ResponsavelTecnicoEmail)) throw new DomainException("E-mail inválido.");
        if (!CadastroBrasileiro.EmailValido(data.DiretorClinicoEmail)) throw new DomainException("E-mail inválido.");
        if (!CadastroBrasileiro.TelefoneValido(data.TelefonePrincipal)) throw new DomainException("Telefone inválido.");
        if (!CadastroBrasileiro.TelefoneValido(data.TelefoneSecundario)) throw new DomainException("Telefone inválido.");
        if (!CadastroBrasileiro.TelefoneValido(data.Whatsapp)) throw new DomainException("Telefone inválido.");
        if (!CadastroBrasileiro.TelefoneValido(data.ResponsavelAdministrativoTelefone)) throw new DomainException("Telefone inválido.");
        if (!CadastroBrasileiro.TelefoneValido(data.ResponsavelTecnicoTelefone)) throw new DomainException("Telefone inválido.");
        if (!CadastroBrasileiro.TelefoneValido(data.DiretorClinicoTelefone)) throw new DomainException("Telefone inválido.");
        if (!CadastroBrasileiro.SiteValido(data.Site)) throw new DomainException("Site deve ser uma URL HTTP ou HTTPS.");
        return data;
    }
}
