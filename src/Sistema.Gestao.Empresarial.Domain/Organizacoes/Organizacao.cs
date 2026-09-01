using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

public sealed class Organizacao : EntidadeAuditavel
{
    private Organizacao()
    {
    }

    public Organizacao(Guid guid, string nome, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 200);
    }

    public string Nome { get; private set; } = string.Empty;
}
