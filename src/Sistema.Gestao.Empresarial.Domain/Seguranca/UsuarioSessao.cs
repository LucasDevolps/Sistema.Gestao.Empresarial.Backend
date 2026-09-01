using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Seguranca;

public sealed class UsuarioSessao : EntidadeAuditavel
{
    private UsuarioSessao()
    {
    }

    public UsuarioSessao(
        Guid guid,
        long usuarioId,
        Guid sessionId,
        string jti,
        string accessTokenHash,
        string refreshTokenHash,
        long versaoSessao,
        DateTimeOffset criadoEm,
        DateTimeOffset expiraEm,
        string? ipOrigem,
        string? userAgent)
        : base(guid, criadoEm)
    {
        if (usuarioId <= 0 || sessionId == Guid.Empty || versaoSessao <= 0)
        {
            throw new DomainException("Usuário, sessão e versão são obrigatórios.");
        }

        UsuarioId = usuarioId;
        SessionId = sessionId;
        Jti = Guard.TextoObrigatorio(jti, nameof(Jti), 100);
        AccessTokenHash = Guard.TextoObrigatorio(accessTokenHash, nameof(AccessTokenHash), 128);
        RefreshTokenHash = Guard.TextoObrigatorio(refreshTokenHash, nameof(RefreshTokenHash), 128);
        VersaoSessao = versaoSessao;
        UltimaAtividadeEm = criadoEm;
        ExpiraEm = expiraEm;
        IpOrigem = ipOrigem?.Trim();
        UserAgent = userAgent?.Trim();
    }

    public long UsuarioId { get; private set; }
    public Guid SessionId { get; private set; }
    public string Jti { get; private set; } = string.Empty;
    public string AccessTokenHash { get; private set; } = string.Empty;
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public long VersaoSessao { get; private set; }
    public DateTimeOffset UltimaAtividadeEm { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public bool Revogado { get; private set; }
    public DateTimeOffset? DataRevogacao { get; private set; }
    public string? MotivoRevogacao { get; private set; }
    public string? IpOrigem { get; private set; }
    public string? UserAgent { get; private set; }
    public Usuario Usuario { get; private set; } = null!;

    public bool Revogar(string motivo, DateTimeOffset ocorridoEm)
    {
        if (Revogado || !Ativo)
        {
            return false;
        }

        Revogado = true;
        DataRevogacao = ocorridoEm;
        MotivoRevogacao = Guard.TextoObrigatorio(motivo, nameof(MotivoRevogacao), 200);
        Inativar(ocorridoEm);
        return true;
    }

    public void RegistrarAtividade(DateTimeOffset ocorridoEm)
    {
        if (ocorridoEm > UltimaAtividadeEm)
        {
            UltimaAtividadeEm = ocorridoEm;
            MarcarAtualizacao(ocorridoEm);
        }
    }

    public void RotacionarTokens(
        string jti,
        string accessTokenHash,
        string refreshTokenHash,
        DateTimeOffset ocorridoEm)
    {
        if (Revogado || !Ativo)
        {
            throw new DomainException("Não é possível renovar uma sessão inativa.");
        }

        Jti = Guard.TextoObrigatorio(jti, nameof(Jti), 100);
        AccessTokenHash = Guard.TextoObrigatorio(accessTokenHash, nameof(AccessTokenHash), 128);
        RefreshTokenHash = Guard.TextoObrigatorio(refreshTokenHash, nameof(RefreshTokenHash), 128);
        RegistrarAtividade(ocorridoEm);
    }
}
