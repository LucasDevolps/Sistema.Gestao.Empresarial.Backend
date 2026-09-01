using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Seguranca;

public sealed class Perfil : EntidadeAuditavel
{
    private Perfil() { }

    public Perfil(Guid guid, string nome, string? descricao, DateTimeOffset criadoEm) : base(guid, criadoEm)
    {
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 100);
        Descricao = descricao?.Trim();
    }

    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
}
