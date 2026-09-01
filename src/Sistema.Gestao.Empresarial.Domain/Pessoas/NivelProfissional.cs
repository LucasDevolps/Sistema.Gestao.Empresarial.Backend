using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Pessoas;

public sealed class NivelProfissional : EntidadeAuditavel
{
    private NivelProfissional()
    {
    }

    public NivelProfissional(Guid guid, string codigo, string nome, int ordem, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        Codigo = Guard.TextoObrigatorio(codigo, nameof(Codigo), 10).ToUpperInvariant();
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 80);
        Ordem = ordem;
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public int Ordem { get; private set; }
}
