using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

/// <summary>
/// Setor hospitalar pertencente a uma unidade principal. Concentra a identificação
/// operacional (sigla + nome), a classificação por <see cref="CategoriaSetor"/>,
/// dados de contato e as regras de participação em escala e atuação compartilhada.
/// As unidades adicionalmente atendidas são vínculos temporais
/// (<see cref="SetorUnidadeAtendida"/>), preservando histórico como os demais
/// vínculos organizacionais.
/// </summary>
public sealed class Setor : EntidadeAuditavel
{
    private Setor()
    {
    }

    public Setor(
        Guid guid,
        long unidadeHospitalarId,
        long categoriaSetorId,
        string nome,
        string sigla,
        string? descricao,
        string? localizacaoInterna,
        string? ramal,
        string? email,
        long? responsavelFuncionarioId,
        bool assistencial,
        bool permiteAlocacaoEscala,
        bool permiteAtuacaoCompartilhada,
        DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        if (unidadeHospitalarId <= 0)
        {
            throw new DomainException("A unidade hospitalar é obrigatória.");
        }

        UnidadeHospitalarId = unidadeHospitalarId;
        CategoriaSetorId = ValidarCategoria(categoriaSetorId);
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 150);
        Sigla = NormalizarSigla(sigla);
        Descricao = Guard.TextoOpcional(descricao, nameof(Descricao), 1000);
        LocalizacaoInterna = Guard.TextoOpcional(localizacaoInterna, nameof(LocalizacaoInterna), 150);
        Ramal = Guard.TextoOpcional(ramal, nameof(Ramal), 30);
        Email = NormalizarEmail(email);
        ResponsavelFuncionarioId = ValidarResponsavel(responsavelFuncionarioId);
        Assistencial = assistencial;
        PermiteAlocacaoEscala = permiteAlocacaoEscala;
        PermiteAtuacaoCompartilhada = permiteAtuacaoCompartilhada;
    }

    /// <summary>Unidade principal responsável pelo cadastro. Imutável após a criação.</summary>
    public long UnidadeHospitalarId { get; private set; }
    public long CategoriaSetorId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Sigla { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public string? LocalizacaoInterna { get; private set; }
    public string? Ramal { get; private set; }
    public string? Email { get; private set; }
    public long? ResponsavelFuncionarioId { get; private set; }
    public bool Assistencial { get; private set; }
    public bool PermiteAlocacaoEscala { get; private set; }
    public bool PermiteAtuacaoCompartilhada { get; private set; }

    public UnidadeHospitalar UnidadeHospitalar { get; private set; } = null!;
    public CategoriaSetor CategoriaSetor { get; private set; } = null!;

    public bool Atualizar(
        long categoriaSetorId,
        string nome,
        string sigla,
        string? descricao,
        string? localizacaoInterna,
        string? ramal,
        string? email,
        long? responsavelFuncionarioId,
        bool assistencial,
        bool permiteAlocacaoEscala,
        bool permiteAtuacaoCompartilhada,
        DateTimeOffset atualizadoEm)
    {
        var novaCategoria = ValidarCategoria(categoriaSetorId);
        var novoNome = Guard.TextoObrigatorio(nome, nameof(Nome), 150);
        var novaSigla = NormalizarSigla(sigla);
        var novaDescricao = Guard.TextoOpcional(descricao, nameof(Descricao), 1000);
        var novaLocalizacao = Guard.TextoOpcional(localizacaoInterna, nameof(LocalizacaoInterna), 150);
        var novoRamal = Guard.TextoOpcional(ramal, nameof(Ramal), 30);
        var novoEmail = NormalizarEmail(email);
        var novoResponsavel = ValidarResponsavel(responsavelFuncionarioId);

        if (CategoriaSetorId == novaCategoria
            && Nome == novoNome
            && Sigla == novaSigla
            && Descricao == novaDescricao
            && LocalizacaoInterna == novaLocalizacao
            && Ramal == novoRamal
            && Email == novoEmail
            && ResponsavelFuncionarioId == novoResponsavel
            && Assistencial == assistencial
            && PermiteAlocacaoEscala == permiteAlocacaoEscala
            && PermiteAtuacaoCompartilhada == permiteAtuacaoCompartilhada)
        {
            return false;
        }

        CategoriaSetorId = novaCategoria;
        Nome = novoNome;
        Sigla = novaSigla;
        Descricao = novaDescricao;
        LocalizacaoInterna = novaLocalizacao;
        Ramal = novoRamal;
        Email = novoEmail;
        ResponsavelFuncionarioId = novoResponsavel;
        Assistencial = assistencial;
        PermiteAlocacaoEscala = permiteAlocacaoEscala;
        PermiteAtuacaoCompartilhada = permiteAtuacaoCompartilhada;
        MarcarAtualizacao(atualizadoEm);
        return true;
    }

    private static long ValidarCategoria(long categoriaSetorId)
    {
        if (categoriaSetorId <= 0)
        {
            throw new DomainException("A categoria do setor é obrigatória.");
        }

        return categoriaSetorId;
    }

    private static long? ValidarResponsavel(long? responsavelFuncionarioId)
    {
        if (responsavelFuncionarioId is <= 0)
        {
            throw new DomainException("O responsável informado é inválido.");
        }

        return responsavelFuncionarioId;
    }

    private static string NormalizarSigla(string sigla)
    {
        var normalizada = Guard.TextoObrigatorio(sigla, nameof(Sigla), 20).ToUpperInvariant();
        return normalizada;
    }

    private static string? NormalizarEmail(string? email)
    {
        var normalizado = Guard.TextoOpcional(email, nameof(Email), 254);
        return normalizado?.ToLowerInvariant();
    }
}
