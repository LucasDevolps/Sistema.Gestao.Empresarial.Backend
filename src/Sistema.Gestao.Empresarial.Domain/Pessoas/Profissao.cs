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
}
