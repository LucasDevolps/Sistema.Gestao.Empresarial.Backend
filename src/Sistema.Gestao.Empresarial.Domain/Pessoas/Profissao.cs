using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Pessoas;

public sealed class Profissao : EntidadeAuditavel
{
    private Profissao()
    {
    }

    public Profissao(Guid guid, string nome, string? descricao, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 150);
        Descricao = descricao?.Trim();
    }

    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    public bool Atualizar(string nome, string? descricao, DateTimeOffset atualizadoEm)
    {
        var novoNome = Guard.TextoObrigatorio(nome, nameof(Nome), 150);
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
