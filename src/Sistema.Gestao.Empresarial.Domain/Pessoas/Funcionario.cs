using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;

namespace Sistema.Gestao.Empresarial.Domain.Pessoas;

public sealed class Funcionario : EntidadeAuditavel
{
    private Funcionario()
    {
    }

    public Funcionario(
        Guid guid,
        string nome,
        string email,
        string? telefone,
        long profissaoId,
        long cargoId,
        long nivelId,
        long unidadeContratacaoId,
        DateOnly dataAdmissao,
        DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 200);
        Email = Guard.TextoObrigatorio(email, nameof(Email), 254).ToLowerInvariant();
        Telefone = NormalizarTelefone(telefone);
        ProfissaoId = ValidarId(profissaoId, "A profissão");
        CargoId = ValidarId(cargoId, "O cargo");
        NivelId = ValidarId(nivelId, "O nível profissional");
        UnidadeContratacaoId = ValidarId(unidadeContratacaoId, "A unidade de contratação");
        DataAdmissao = dataAdmissao;
    }

    public string Matricula { get; private set; } = null!;
    public string Nome { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Telefone { get; private set; }
    public long ProfissaoId { get; private set; }
    public long CargoId { get; private set; }
    public long NivelId { get; private set; }
    public long UnidadeContratacaoId { get; private set; }
    public DateOnly DataAdmissao { get; private set; }
    public Profissao Profissao { get; private set; } = null!;
    public Cargo Cargo { get; private set; } = null!;
    public NivelProfissional Nivel { get; private set; } = null!;
    public UnidadeHospitalar UnidadeContratacao { get; private set; } = null!;

    public bool AtualizarDados(
        string nome,
        string email,
        string? telefone,
        long profissaoId,
        long cargoId,
        long nivelId,
        DateTimeOffset atualizadoEm)
    {
        var novoNome = Guard.TextoObrigatorio(nome, nameof(Nome), 200);
        var novoEmail = Guard.TextoObrigatorio(email, nameof(Email), 254).ToLowerInvariant();
        var novoTelefone = NormalizarTelefone(telefone);
        profissaoId = ValidarId(profissaoId, "A profissão");
        cargoId = ValidarId(cargoId, "O cargo");
        nivelId = ValidarId(nivelId, "O nível profissional");

        if (Nome == novoNome
            && Email == novoEmail
            && Telefone == novoTelefone
            && ProfissaoId == profissaoId
            && CargoId == cargoId
            && NivelId == nivelId)
        {
            return false;
        }

        Nome = novoNome;
        Email = novoEmail;
        Telefone = novoTelefone;
        ProfissaoId = profissaoId;
        CargoId = cargoId;
        NivelId = nivelId;
        MarcarAtualizacao(atualizadoEm);
        return true;
    }

    private static long ValidarId(long id, string nome)
    {
        if (id <= 0)
        {
            throw new DomainException($"{nome} é obrigatório(a).");
        }

        return id;
    }

    private static string? NormalizarTelefone(string? telefone)
    {
        var normalizado = telefone?.Trim();
        if (string.IsNullOrEmpty(normalizado))
        {
            return null;
        }

        if (normalizado.Length > 30)
        {
            throw new DomainException("Telefone deve possuir no máximo 30 caracteres.");
        }

        return normalizado;
    }
}
