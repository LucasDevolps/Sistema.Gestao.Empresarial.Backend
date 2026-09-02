using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;

namespace Sistema.Gestao.Empresarial.Domain.Pessoas;

public sealed class FuncionarioUnidadeAtuacao : EntidadeAuditavel
{
    private FuncionarioUnidadeAtuacao()
    {
    }

    public FuncionarioUnidadeAtuacao(
        Guid guid,
        long funcionarioId,
        long unidadeHospitalarId,
        DateOnly dataInicio,
        DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        if (funcionarioId <= 0 || unidadeHospitalarId <= 0)
        {
            throw new DomainException("Funcionário e unidade de atuação são obrigatórios.");
        }

        FuncionarioId = funcionarioId;
        UnidadeHospitalarId = unidadeHospitalarId;
        DataInicio = dataInicio;
    }

    public long FuncionarioId { get; private set; }
    public long UnidadeHospitalarId { get; private set; }
    public DateOnly DataInicio { get; private set; }
    public DateOnly? DataFim { get; private set; }
    public Funcionario Funcionario { get; private set; } = null!;
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
