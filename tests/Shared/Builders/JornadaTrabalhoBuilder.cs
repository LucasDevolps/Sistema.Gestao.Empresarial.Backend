using Sistema.Gestao.Empresarial.Domain.Escalas;

namespace Sistema.Gestao.Empresarial.Tests.Builders;

public sealed class JornadaTrabalhoBuilder
{
    public static readonly DateTimeOffset CriadoEmPadrao = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private Guid _guid = Guid.NewGuid();
    private string _nome = "6x1";
    private int _diasTrabalho = 6;
    private int _diasDescanso = 1;
    private int _maximo = 6;
    private string? _descricao;
    private DateTimeOffset _criadoEm = CriadoEmPadrao;

    public JornadaTrabalhoBuilder ComGuid(Guid guid)
    {
        _guid = guid;
        return this;
    }

    public JornadaTrabalhoBuilder ComNome(string nome)
    {
        _nome = nome;
        return this;
    }

    public JornadaTrabalhoBuilder ComCiclo(int diasTrabalho, int diasDescanso)
    {
        _diasTrabalho = diasTrabalho;
        _diasDescanso = diasDescanso;
        return this;
    }

    public JornadaTrabalhoBuilder ComDiasTrabalho(int diasTrabalho)
    {
        _diasTrabalho = diasTrabalho;
        return this;
    }

    public JornadaTrabalhoBuilder ComDiasDescanso(int diasDescanso)
    {
        _diasDescanso = diasDescanso;
        return this;
    }

    public JornadaTrabalhoBuilder ComMaximo(int maximo)
    {
        _maximo = maximo;
        return this;
    }

    public JornadaTrabalhoBuilder ComDescricao(string? descricao)
    {
        _descricao = descricao;
        return this;
    }

    public JornadaTrabalho Build() =>
        new(_guid, _nome, _diasTrabalho, _diasDescanso, _maximo, _descricao, _criadoEm);
}
