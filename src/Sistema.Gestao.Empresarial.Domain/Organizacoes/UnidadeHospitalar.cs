using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

public sealed class UnidadeHospitalar : EntidadeAuditavel
{
    private UnidadeHospitalar()
    {
    }

    public UnidadeHospitalar(Guid guid, long organizacaoId, string nome, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        if (organizacaoId <= 0)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        OrganizacaoId = organizacaoId;
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 200);
    }

    /// <summary>Cadastro completo; o construtor anterior permanece para bootstrap e dados legados.</summary>
    public UnidadeHospitalar(Guid guid, long organizacaoId, CadastroUnidadeHospitalar cadastro, DateTimeOffset criadoEm)
        : this(guid, organizacaoId, cadastro.Nome, criadoEm) => AtualizarCadastro(cadastro, criadoEm);

    public void AtualizarCadastro(CadastroUnidadeHospitalar cadastro, DateTimeOffset atualizadoEm)
    {
        var data = cadastro.NormalizarEValidar();
        Nome = data.Nome;
        RazaoSocial = data.RazaoSocial;
        Cnpj = data.Cnpj;
        PossuiCnpjProprio = data.PossuiCnpjProprio;
        Cnes = data.Cnes;
        Tipo = data.Tipo;
        Natureza = data.Natureza;
        CodigoInterno = data.CodigoInterno;
        Sigla = data.Sigla;
        InicioAtividades = data.InicioAtividades;
        SituacaoCadastral = data.SituacaoCadastral;
        DataAbertura = data.DataAbertura;
        NaturezaJuridica = data.NaturezaJuridica;
        CnaePrincipal = data.CnaePrincipal;
        CnaesSecundarios = data.CnaesSecundarios;
        InscricaoEstadual = data.InscricaoEstadual;
        InscricaoMunicipal = data.InscricaoMunicipal;
        Cep = data.Cep;
        Logradouro = data.Logradouro;
        Numero = data.Numero;
        Complemento = data.Complemento;
        Bairro = data.Bairro;
        Cidade = data.Cidade;
        Uf = data.Uf;
        CodigoIbge = data.CodigoIbge;
        Regiao = data.Regiao;
        Ddd = data.Ddd;
        ReferenciaEndereco = data.ReferenciaEndereco;
        TelefonePrincipal = data.TelefonePrincipal;
        TelefoneSecundario = data.TelefoneSecundario;
        Whatsapp = data.Whatsapp;
        EmailInstitucional = data.EmailInstitucional;
        EmailAdministrativo = data.EmailAdministrativo;
        Site = data.Site;
        Ramal = data.Ramal;
        ResponsavelAdministrativoNome = data.ResponsavelAdministrativoNome;
        ResponsavelAdministrativoCargo = data.ResponsavelAdministrativoCargo;
        ResponsavelAdministrativoEmail = data.ResponsavelAdministrativoEmail;
        ResponsavelAdministrativoTelefone = data.ResponsavelAdministrativoTelefone;
        ResponsavelTecnicoNome = data.ResponsavelTecnicoNome;
        ResponsavelTecnicoProfissao = data.ResponsavelTecnicoProfissao;
        ResponsavelTecnicoConselho = data.ResponsavelTecnicoConselho;
        ResponsavelTecnicoRegistro = data.ResponsavelTecnicoRegistro;
        ResponsavelTecnicoUf = data.ResponsavelTecnicoUf;
        ResponsavelTecnicoEmail = data.ResponsavelTecnicoEmail;
        ResponsavelTecnicoTelefone = data.ResponsavelTecnicoTelefone;
        DiretorClinicoNome = data.DiretorClinicoNome;
        DiretorClinicoCrm = data.DiretorClinicoCrm;
        DiretorClinicoUf = data.DiretorClinicoUf;
        DiretorClinicoEmail = data.DiretorClinicoEmail;
        DiretorClinicoTelefone = data.DiretorClinicoTelefone;
        AlvaraSanitario = data.AlvaraSanitario;
        ValidadeAlvaraSanitario = data.ValidadeAlvaraSanitario;
        LicencaFuncionamento = data.LicencaFuncionamento;
        ValidadeLicencaFuncionamento = data.ValidadeLicencaFuncionamento;
        ObservacoesRegulatorias = data.ObservacoesRegulatorias;
        Atendimento24h = data.Atendimento24h;
        ProntoSocorro = data.ProntoSocorro;
        Internacao = data.Internacao;
        Uti = data.Uti;
        TotalLeitos = data.TotalLeitos;
        LeitosUti = data.LeitosUti;
        CentroCirurgico = data.CentroCirurgico;
        Maternidade = data.Maternidade;
        AtendimentoAmbulatorial = data.AtendimentoAmbulatorial;
        ObservacoesGerais = data.ObservacoesGerais;
        MarcarAtualizacao(atualizadoEm);
    }

    public string? RazaoSocial { get; private set; }
    public string? Cnpj { get; private set; }
    public bool PossuiCnpjProprio { get; private set; }
    public string? Cnes { get; private set; }
    public TipoUnidadeHospitalar? Tipo { get; private set; }
    public NaturezaUnidadeHospitalar? Natureza { get; private set; }
    public string? CodigoInterno { get; private set; }
    public string? Sigla { get; private set; }
    public DateOnly? InicioAtividades { get; private set; }
    public string? SituacaoCadastral { get; private set; }
    public DateOnly? DataAbertura { get; private set; }
    public string? NaturezaJuridica { get; private set; }
    public string? CnaePrincipal { get; private set; }
    public string? CnaesSecundarios { get; private set; }
    public string? InscricaoEstadual { get; private set; }
    public string? InscricaoMunicipal { get; private set; }
    public string? Cep { get; private set; }
    public string? Logradouro { get; private set; }
    public string? Numero { get; private set; }
    public string? Complemento { get; private set; }
    public string? Bairro { get; private set; }
    public string? Cidade { get; private set; }
    public string? Uf { get; private set; }
    public string? CodigoIbge { get; private set; }
    public string? Regiao { get; private set; }
    public string? Ddd { get; private set; }
    public string? ReferenciaEndereco { get; private set; }
    public string? TelefonePrincipal { get; private set; }
    public string? TelefoneSecundario { get; private set; }
    public string? Whatsapp { get; private set; }
    public string? EmailInstitucional { get; private set; }
    public string? EmailAdministrativo { get; private set; }
    public string? Site { get; private set; }
    public string? Ramal { get; private set; }
    public string? ResponsavelAdministrativoNome { get; private set; }
    public string? ResponsavelAdministrativoCargo { get; private set; }
    public string? ResponsavelAdministrativoEmail { get; private set; }
    public string? ResponsavelAdministrativoTelefone { get; private set; }
    public string? ResponsavelTecnicoNome { get; private set; }
    public string? ResponsavelTecnicoProfissao { get; private set; }
    public string? ResponsavelTecnicoConselho { get; private set; }
    public string? ResponsavelTecnicoRegistro { get; private set; }
    public string? ResponsavelTecnicoUf { get; private set; }
    public string? ResponsavelTecnicoEmail { get; private set; }
    public string? ResponsavelTecnicoTelefone { get; private set; }
    public string? DiretorClinicoNome { get; private set; }
    public string? DiretorClinicoCrm { get; private set; }
    public string? DiretorClinicoUf { get; private set; }
    public string? DiretorClinicoEmail { get; private set; }
    public string? DiretorClinicoTelefone { get; private set; }
    public string? AlvaraSanitario { get; private set; }
    public DateOnly? ValidadeAlvaraSanitario { get; private set; }
    public string? LicencaFuncionamento { get; private set; }
    public DateOnly? ValidadeLicencaFuncionamento { get; private set; }
    public string? ObservacoesRegulatorias { get; private set; }
    public bool? Atendimento24h { get; private set; }
    public bool? ProntoSocorro { get; private set; }
    public bool? Internacao { get; private set; }
    public bool? Uti { get; private set; }
    public int? TotalLeitos { get; private set; }
    public int? LeitosUti { get; private set; }
    public bool? CentroCirurgico { get; private set; }
    public bool? Maternidade { get; private set; }
    public bool? AtendimentoAmbulatorial { get; private set; }
    public string? ObservacoesGerais { get; private set; }

    public long OrganizacaoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Organizacao Organizacao { get; private set; } = null!;
}
