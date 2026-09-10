using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

/// <summary>
/// Vínculo temporal entre um <see cref="Setor"/> de atuação compartilhada e uma
/// unidade hospitalar adicionalmente atendida, distinta da unidade principal.
/// Preserva histórico: é encerrado/inativado, nunca removido.
/// </summary>
public sealed class SetorUnidadeAtendida : EntidadeAuditavel
{
    private SetorUnidadeAtendida()
    {
    }

    public SetorUnidadeAtendida(
        Guid guid,
        long setorId,
        long unidadeHospitalarId,
        DateOnly dataInicio,
        DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        if (setorId <= 0 || unidadeHospitalarId <= 0)
        {
            throw new DomainException("Setor e unidade atendida são obrigatórios.");
        }

        SetorId = setorId;
        UnidadeHospitalarId = unidadeHospitalarId;
        DataInicio = dataInicio;
    }

    public long SetorId { get; private set; }
    public long UnidadeHospitalarId { get; private set; }
    public DateOnly DataInicio { get; private set; }
    public DateOnly? DataFim { get; private set; }
    public Setor Setor { get; private set; } = null!;
    public UnidadeHospitalar UnidadeHospitalar { get; private set; } = null!;

    public bool Encerrar(DateOnly dataFim, DateTimeOffset atualizadoEm)
    {
        if (!Ativo)
        {
            return false;
        }

        if (dataFim < DataInicio)
        {
            throw new DomainException("A data final não pode ser anterior à data inicial.");
        }

        DataFim = dataFim;
        Inativar(atualizadoEm);
        return true;
    }
}
