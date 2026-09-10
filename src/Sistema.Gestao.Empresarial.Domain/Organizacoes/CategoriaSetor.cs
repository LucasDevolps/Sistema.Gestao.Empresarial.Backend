using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

/// <summary>
/// Catálogo configurável que classifica setores hospitalares por finalidade
/// (assistencial, administrativo, diagnóstico, cirúrgico, ...). Segue o mesmo
/// ciclo de vida sem DELETE de <see cref="Profissao"/> e <see cref="Cargo"/>:
/// identidade própria, nome como chave de negócio e inativação lógica.
/// </summary>
public sealed class CategoriaSetor : EntidadeAuditavel
{
    private CategoriaSetor()
    {
    }

    public CategoriaSetor(Guid guid, string nome, string? descricao, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 120);
        Descricao = descricao?.Trim();
    }

    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    public bool Atualizar(string nome, string? descricao, DateTimeOffset atualizadoEm)
    {
        var novoNome = Guard.TextoObrigatorio(nome, nameof(Nome), 120);
        var novaDescricao = descricao?.Trim();
        if (Nome == novoNome && Descricao == novaDescricao)
        {
            return false;
        }

        Nome = novoNome;
        Descricao = novaDescricao;
        MarcarAtualizacao(atualizadoEm);
        return true;
    }
}
