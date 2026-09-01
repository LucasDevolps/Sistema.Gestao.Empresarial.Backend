using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Seguranca;

public sealed class Permissao : EntidadeAuditavel
{
    private Permissao() { }

    public Permissao(Guid guid, string codigo, string descricao, DateTimeOffset criadoEm) : base(guid, criadoEm)
    {
        Codigo = Guard.TextoObrigatorio(codigo, nameof(Codigo), 150).ToUpperInvariant();
        Descricao = Guard.TextoObrigatorio(descricao, nameof(Descricao), 300);
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
}
