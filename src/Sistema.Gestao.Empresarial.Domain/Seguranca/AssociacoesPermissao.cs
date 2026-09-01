using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Seguranca;

public sealed class PerfilPermissao : EntidadeAuditavel
{
    private PerfilPermissao() { }
    public PerfilPermissao(Guid guid, long perfilId, long permissaoId, DateTimeOffset criadoEm) : base(guid, criadoEm)
    {
        PerfilId = perfilId;
        PermissaoId = permissaoId;
    }
    public long PerfilId { get; private set; }
    public long PermissaoId { get; private set; }
    public Perfil Perfil { get; private set; } = null!;
    public Permissao Permissao { get; private set; } = null!;
}

public sealed class UsuarioPerfil : EntidadeAuditavel
{
    private UsuarioPerfil() { }
    public UsuarioPerfil(Guid guid, long usuarioId, long perfilId, DateTimeOffset criadoEm) : base(guid, criadoEm)
    {
        UsuarioId = usuarioId;
        PerfilId = perfilId;
    }
    public long UsuarioId { get; private set; }
    public long PerfilId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public Perfil Perfil { get; private set; } = null!;
}

public sealed class UsuarioPermissao : EntidadeAuditavel
{
    private UsuarioPermissao() { }
    public UsuarioPermissao(Guid guid, long usuarioId, long permissaoId, bool concedida, DateTimeOffset criadoEm) : base(guid, criadoEm)
    {
        UsuarioId = usuarioId;
        PermissaoId = permissaoId;
        Concedida = concedida;
    }
    public long UsuarioId { get; private set; }
    public long PermissaoId { get; private set; }
    public bool Concedida { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public Permissao Permissao { get; private set; } = null!;

    public bool AlterarConcessao(bool concedida, DateTimeOffset ocorridoEm)
    {
        if (Concedida == concedida && Ativo)
        {
            return false;
        }

        Concedida = concedida;
        Reativar(ocorridoEm);
        MarcarAtualizacao(ocorridoEm);
        return true;
    }
}
