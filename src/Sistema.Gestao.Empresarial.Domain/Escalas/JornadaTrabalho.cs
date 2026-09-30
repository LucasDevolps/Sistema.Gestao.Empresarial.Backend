using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Escalas;

/// <summary>
/// Tipo de jornada de trabalho configurável pela instituição (por exemplo, 6x1,
/// 5x2, 12x36), consumido futuramente pelo módulo de escalas. O comportamento
/// depende apenas dos parâmetros numéricos — nunca do nome — e o cadastro não
/// contém nenhum modelo fixo. É um catálogo global, como cargos e níveis
/// profissionais. O nome é a chave de negócio. O ciclo de vida termina na
/// exclusão lógica herdada de <see cref="EntidadeAuditavel"/>: a jornada nunca é
/// removida fisicamente, pois poderá ser referenciada por histórico.
/// </summary>
public sealed class JornadaTrabalho : EntidadeAuditavel
{
    public const int NomeTamanhoMaximo = 100;
    public const int DescricaoTamanhoMaximo = 500;

    /// <summary>Um funcionário nunca pode trabalhar 7 ou mais dias seguidos.</summary>
    public const int MaximoDiasConsecutivosPermitido = 6;

    private JornadaTrabalho()
    {
    }

    public JornadaTrabalho(
        Guid guid,
        string nome,
        int diasConsecutivosTrabalho,
        int diasDescansoCiclo,
        int maximoDiasConsecutivos,
        string? descricao,
        DateTimeOffset criadoEm)
        : base(guid, criadoEm)
    {
        Nome = Guard.TextoObrigatorio(nome, nameof(Nome), NomeTamanhoMaximo);
        DiasConsecutivosTrabalho = ValidarDiasPositivos(diasConsecutivosTrabalho, nameof(DiasConsecutivosTrabalho));
        DiasDescansoCiclo = ValidarDiasPositivos(diasDescansoCiclo, nameof(DiasDescansoCiclo));
        MaximoDiasConsecutivos = ValidarMaximoConsecutivo(maximoDiasConsecutivos);
        Descricao = Guard.TextoOpcional(descricao, nameof(Descricao), DescricaoTamanhoMaximo);
    }

    public string Nome { get; private set; } = string.Empty;
    public int DiasConsecutivosTrabalho { get; private set; }
    public int DiasDescansoCiclo { get; private set; }
    public int MaximoDiasConsecutivos { get; private set; }
    public string? Descricao { get; private set; }

    public bool Atualizar(
        string nome,
        int diasConsecutivosTrabalho,
        int diasDescansoCiclo,
        int maximoDiasConsecutivos,
        string? descricao,
        DateTimeOffset atualizadoEm)
    {
        if (Excluido)
        {
            throw new DomainException("Uma jornada de trabalho excluída não pode ser alterada.");
        }

        var novoNome = Guard.TextoObrigatorio(nome, nameof(Nome), NomeTamanhoMaximo);
        var novosDiasTrabalho = ValidarDiasPositivos(diasConsecutivosTrabalho, nameof(DiasConsecutivosTrabalho));
        var novosDiasDescanso = ValidarDiasPositivos(diasDescansoCiclo, nameof(DiasDescansoCiclo));
        var novoMaximo = ValidarMaximoConsecutivo(maximoDiasConsecutivos);
        var novaDescricao = Guard.TextoOpcional(descricao, nameof(Descricao), DescricaoTamanhoMaximo);
        if (Nome == novoNome
            && DiasConsecutivosTrabalho == novosDiasTrabalho
            && DiasDescansoCiclo == novosDiasDescanso
            && MaximoDiasConsecutivos == novoMaximo
            && Descricao == novaDescricao)
        {
            return false;
        }

        Nome = novoNome;
        DiasConsecutivosTrabalho = novosDiasTrabalho;
        DiasDescansoCiclo = novosDiasDescanso;
        MaximoDiasConsecutivos = novoMaximo;
        Descricao = novaDescricao;
        MarcarAtualizacao(atualizadoEm);
        return true;
    }

    private static int ValidarDiasPositivos(int dias, string campo)
    {
        if (dias <= 0)
        {
            throw new DomainException($"{campo} deve ser maior que zero.");
        }

        return dias;
    }

    private static int ValidarMaximoConsecutivo(int maximo)
    {
        if (maximo is < 1 or > MaximoDiasConsecutivosPermitido)
        {
            throw new DomainException(
                $"{nameof(MaximoDiasConsecutivos)} deve estar entre 1 e {MaximoDiasConsecutivosPermitido}.");
        }

        return maximo;
    }
}
