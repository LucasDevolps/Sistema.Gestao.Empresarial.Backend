using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

public sealed class UnidadeHospitalar : EntidadeAuditavel
{
    private UnidadeHospitalar()
    {
    }

    public UnidadeHospitalar(Guid guid, long organizacaoId, string nome, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        if (organizacaoId <= 0)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        OrganizacaoId = organizacaoId;
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 200);
    }

    public long OrganizacaoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Organizacao Organizacao { get; private set; } = null!;
}
