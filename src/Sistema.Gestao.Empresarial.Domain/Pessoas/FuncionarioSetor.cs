using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;

namespace Sistema.Gestao.Empresarial.Domain.Pessoas;

public sealed class FuncionarioSetor : EntidadeAuditavel
{
    private FuncionarioSetor()
    {
    }

    public FuncionarioSetor(Guid guid, long funcionarioId, long setorId, DateOnly dataInicio, DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        if (funcionarioId <= 0 || setorId <= 0)
        {
            throw new DomainException("Funcionário e setor são obrigatórios.");
        }

        FuncionarioId = funcionarioId;
        SetorId = setorId;
        DataInicio = dataInicio;
    }

    public long FuncionarioId { get; private set; }
    public long SetorId { get; private set; }
    public DateOnly DataInicio { get; private set; }
    public DateOnly? DataFim { get; private set; }
    public Funcionario Funcionario { get; private set; } = null!;
    public Setor Setor { get; private set; } = null!;

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
