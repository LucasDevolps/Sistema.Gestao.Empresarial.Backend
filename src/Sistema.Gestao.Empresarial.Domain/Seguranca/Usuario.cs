using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Pessoas;

namespace Sistema.Gestao.Empresarial.Domain.Seguranca;

public sealed class Usuario : EntidadeAuditavel
{
    private Usuario()
    {
    }

    public Usuario(Guid guid, long? funcionarioId, string email, string senhaHash, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        FuncionarioId = funcionarioId;
        Email = Guard.TextoObrigatorio(email, nameof(Email), 254).ToLowerInvariant();
        SenhaHash = Guard.TextoObrigatorio(senhaHash, nameof(SenhaHash), 1000);
    }

    public long? FuncionarioId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string SenhaHash { get; private set; } = string.Empty;
    public bool Bloqueado { get; private set; }
    public DateTimeOffset? BloqueadoAte { get; private set; }
    public int TentativasLoginInvalidas { get; private set; }
    public DateTimeOffset? DataUltimoLogin { get; private set; }
    public long VersaoSessao { get; private set; }
    public long VersaoPermissoes { get; private set; }
    public Funcionario? Funcionario { get; private set; }

    public void RegistrarLoginValido(DateTimeOffset ocorridoEm)
    {
        TentativasLoginInvalidas = 0;
        Bloqueado = false;
        BloqueadoAte = null;
        DataUltimoLogin = ocorridoEm;
        VersaoSessao++;
        MarcarAtualizacao(ocorridoEm);
    }

    public void RegistrarLoginInvalido(
        DateTimeOffset ocorridoEm,
        int limiteTentativas,
        TimeSpan duracaoBloqueio)
    {
        TentativasLoginInvalidas++;
        if (TentativasLoginInvalidas >= limiteTentativas)
        {
            Bloqueado = true;
            BloqueadoAte = ocorridoEm.Add(duracaoBloqueio);
            TentativasLoginInvalidas = 0;
        }

        MarcarAtualizacao(ocorridoEm);
    }

    public bool EstaTemporariamenteBloqueado(DateTimeOffset ocorridoEm) =>
        Bloqueado && BloqueadoAte > ocorridoEm;

    public void AlterarSenhaHash(string senhaHash, DateTimeOffset ocorridoEm)
    {
        SenhaHash = Guard.TextoObrigatorio(senhaHash, nameof(SenhaHash), 1000);
        VersaoSessao++;
        MarcarAtualizacao(ocorridoEm);
    }

    public long IncrementarVersaoPermissoes(DateTimeOffset ocorridoEm)
    {
        VersaoPermissoes++;
        MarcarAtualizacao(ocorridoEm);
        return VersaoPermissoes;
    }
}
