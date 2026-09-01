using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

public sealed class Setor : EntidadeAuditavel
{
    private Setor()
    {
    }

    public Setor(Guid guid, long unidadeHospitalarId, string nome, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        if (unidadeHospitalarId <= 0)
        {
            throw new DomainException("A unidade hospitalar é obrigatória.");
        }

        UnidadeHospitalarId = unidadeHospitalarId;
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), 150);
    }

    public long UnidadeHospitalarId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public UnidadeHospitalar UnidadeHospitalar { get; private set; } = null!;
}
