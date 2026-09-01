namespace Sistema.Gestao.Empresarial.Domain.Common;

public abstract class EntidadeBase
{
    protected EntidadeBase()
    {
    }

    protected EntidadeBase(Guid guid)
    {
        if (guid == Guid.Empty)
        {
            throw new DomainException("O identificador público não pode ser vazio.");
        }

        Guid = guid;
    }

    public long Id { get; private set; }
    public Guid Guid { get; private set; } = Guid.NewGuid();
}
