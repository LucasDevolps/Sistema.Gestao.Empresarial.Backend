namespace Sistema.Gestao.Empresarial.Domain.Common;

public interface IEntidadeExcluivel
{
    bool Excluido { get; }
    DateTimeOffset? ExcluidoEm { get; }
    Guid? ExcluidoPor { get; }
}
