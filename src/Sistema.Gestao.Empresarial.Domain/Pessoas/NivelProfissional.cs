using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Pessoas;

/// <summary>
/// Catálogo configurável de níveis profissionais (por exemplo, Júnior, Pleno,
/// Sênior) mantido pelo gestor. Código e nome são chaves de negócio; a ordem
/// define a apresentação e pode se repetir (o nome desempata a ordenação).
/// O ciclo de vida termina na exclusão lógica herdada de
/// <see cref="EntidadeAuditavel"/> — o registro nunca é removido fisicamente.
/// </summary>
public sealed class NivelProfissional : EntidadeAuditavel
{
    public const int CodigoTamanhoMaximo = 10;
    public const int NomeTamanhoMaximo = 80;
    public const int OrdemMinima = 1;
    public const int OrdemMaxima = 9999;

    private NivelProfissional()
    {
    }

    public NivelProfissional(Guid guid, string codigo, string nome, int ordem, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        Codigo = NormalizarCodigo(codigo);
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), NomeTamanhoMaximo);
        Ordem = ValidarOrdem(ordem);
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public int Ordem { get; private set; }

    /// <summary>
    /// Normalização canônica do código: sem espaços nas extremidades e em
    /// maiúsculas. Usada também pela pré-checagem de duplicidade.
    /// </summary>
    public static string NormalizarCodigo(string? codigo) =>
        Guard.TextoObrigatorio(codigo, nameof(Codigo), CodigoTamanhoMaximo).ToUpperInvariant();

    public bool Atualizar(string codigo, string nome, int ordem, DateTimeOffset atualizadoEm)
    {
        if (Excluido)
        {
            throw new DomainException("Um nível profissional excluído não pode ser alterado.");
        }

        var novoCodigo = NormalizarCodigo(codigo);
        var novoNome = Guard.TextoObrigatorio(nome, nameof(Nome), NomeTamanhoMaximo);
        var novaOrdem = ValidarOrdem(ordem);
        if (Codigo == novoCodigo && Nome == novoNome && Ordem == novaOrdem)
        {
            return false;
        }

        Codigo = novoCodigo;
        Nome = novoNome;
        Ordem = novaOrdem;
        MarcarAtualizacao(atualizadoEm);
        return true;
    }

    private static int ValidarOrdem(int ordem)
    {
        if (ordem is < OrdemMinima or > OrdemMaxima)
        {
            throw new DomainException($"Ordem deve estar entre {OrdemMinima} e {OrdemMaxima}.");
        }

        return ordem;
    }
}
