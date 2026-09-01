namespace Sistema.Gestao.Empresarial.Domain.Common;

public abstract class EntidadeAuditavel : EntidadeBase, IEntidadeExcluivel
{
    protected EntidadeAuditavel()
    {
    }

    protected EntidadeAuditavel(Guid guid, DateTimeOffset criadoEm)
        : base(guid)
    {
        DataCriacao = criadoEm;
        DataAtualizacao = criadoEm;
    }

    public bool Ativo { get; protected set; } = true;
    public bool Excluido { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }
    public DateTimeOffset? ExcluidoEm { get; private set; }
    public Guid? ExcluidoPor { get; private set; }
    public byte[] Versao { get; private set; } = [];

    public void MarcarAtualizacao(DateTimeOffset atualizadoEm)
    {
        DataAtualizacao = atualizadoEm;
    }

    public virtual void Inativar(DateTimeOffset atualizadoEm)
    {
        if (!Ativo)
            return;

        Ativo = false;
        DataAtualizacao = atualizadoEm;
    }

    public virtual void Reativar(DateTimeOffset atualizadoEm)
    {
        if (Ativo || Excluido)
            return;

        Ativo = true;
        DataAtualizacao = atualizadoEm;
    }

    public void ExcluirLogicamente(Guid ator, DateTimeOffset excluidoEm)
    {
        if (Excluido)
            return;

        if (ator == Guid.Empty)
            throw new DomainException("O responsável pela exclusão lógica é obrigatório.");

        Ativo = false;
        Excluido = true;
        ExcluidoEm = excluidoEm;
        ExcluidoPor = ator;
        DataAtualizacao = excluidoEm;
    }
}
