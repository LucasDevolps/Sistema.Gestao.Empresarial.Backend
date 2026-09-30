using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Escalas;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class JornadaTrabalhoTests
{
    private static readonly DateTimeOffset CriadoEm = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static JornadaTrabalho Nova(
        string nome = "6x1",
        int trabalho = 6,
        int descanso = 1,
        int maximo = 6,
        string? descricao = null) =>
        new(Guid.NewGuid(), nome, trabalho, descanso, maximo, descricao, CriadoEm);

    [Fact]
    public void Criar_6x1_DevePersistirParametros()
    {
        var jornada = Nova("6x1", 6, 1, 6, "Jornada assistencial 6x1");

        Assert.Equal("6x1", jornada.Nome);
        Assert.Equal(6, jornada.DiasConsecutivosTrabalho);
        Assert.Equal(1, jornada.DiasDescansoCiclo);
        Assert.Equal(6, jornada.MaximoDiasConsecutivos);
        Assert.Equal("Jornada assistencial 6x1", jornada.Descricao);
        Assert.True(jornada.Ativo);
        Assert.False(jornada.Excluido);
    }

    [Fact]
    public void Criar_5x2_DevePersistirParametros()
    {
        var jornada = Nova("5x2", 5, 2, 5);

        Assert.Equal(5, jornada.DiasConsecutivosTrabalho);
        Assert.Equal(2, jornada.DiasDescansoCiclo);
        Assert.Equal(5, jornada.MaximoDiasConsecutivos);
        Assert.Null(jornada.Descricao);
    }

    [Fact]
    public void Criar_JornadaNaoPredefinida_DeveSerAceitaSemDependerDoNome()
    {
        var jornada = Nova("Plantão 4x2 noturno", 4, 2, 4);

        Assert.Equal(4, jornada.DiasConsecutivosTrabalho);
        Assert.Equal(2, jornada.DiasDescansoCiclo);
    }

    [Fact]
    public void Criar_DeveAplicarTrimNoNomeENaDescricao_EDescricaoVaziaViraNula()
    {
        Assert.Equal("6x1", Nova("  6x1 ").Nome);
        Assert.Equal("texto", Nova(descricao: "  texto ").Descricao);
        Assert.Null(Nova(descricao: "   ").Descricao);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Criar_NomeObrigatorio(string? nome)
    {
        Assert.Throws<DomainException>(() => Nova(nome!));
    }

    [Fact]
    public void Criar_NomeEDescricaoAcimaDoLimite_DevemSerRejeitados()
    {
        Assert.Throws<DomainException>(() => Nova(new string('N', JornadaTrabalho.NomeTamanhoMaximo + 1)));
        Assert.Throws<DomainException>(() =>
            Nova(descricao: new string('D', JornadaTrabalho.DescricaoTamanhoMaximo + 1)));
        Assert.NotNull(Nova(new string('N', JornadaTrabalho.NomeTamanhoMaximo)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_DiasDeTrabalhoInvalidos_DevemSerRejeitados(int dias)
    {
        Assert.Throws<DomainException>(() => Nova(trabalho: dias));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_DiasDeDescansoInvalidos_DevemSerRejeitados(int dias)
    {
        Assert.Throws<DomainException>(() => Nova(descanso: dias));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(8)]
    public void Criar_MaximoConsecutivoInvalido_DeveSerRejeitado(int maximo)
    {
        Assert.Throws<DomainException>(() => Nova(maximo: maximo));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void Criar_MaximoConsecutivoNosLimites_DeveSerPermitido(int maximo)
    {
        Assert.Equal(maximo, Nova(maximo: maximo).MaximoDiasConsecutivos);
    }

    [Fact]
    public void Criar_GuidVazio_DeveSerRejeitado()
    {
        Assert.Throws<DomainException>(() => new JornadaTrabalho(Guid.Empty, "6x1", 6, 1, 6, null, CriadoEm));
    }

    [Fact]
    public void Atualizar_DevePreservarGuidEAplicarNovosValores()
    {
        var jornada = Nova();
        var guid = jornada.Guid;
        var atualizadoEm = CriadoEm.AddHours(1);

        var alterou = jornada.Atualizar("5x2", 5, 2, 5, "Administrativo", atualizadoEm);

        Assert.True(alterou);
        Assert.Equal(guid, jornada.Guid);
        Assert.Equal("5x2", jornada.Nome);
        Assert.Equal(5, jornada.DiasConsecutivosTrabalho);
        Assert.Equal(2, jornada.DiasDescansoCiclo);
        Assert.Equal(5, jornada.MaximoDiasConsecutivos);
        Assert.Equal("Administrativo", jornada.Descricao);
        Assert.Equal(atualizadoEm, jornada.DataAtualizacao);
    }

    [Fact]
    public void Atualizar_SemMudancas_NaoDeveAlterarNada()
    {
        var jornada = Nova();

        Assert.False(jornada.Atualizar(" 6x1 ", 6, 1, 6, null, CriadoEm.AddHours(1)));
        Assert.Equal(CriadoEm, jornada.DataAtualizacao);
    }

    [Theory]
    [InlineData(0, 1, 6)]
    [InlineData(6, 0, 6)]
    [InlineData(6, 1, 7)]
    [InlineData(6, 1, 0)]
    public void Atualizar_ValoresInvalidos_DevemSerRejeitadosSemAlterarEstado(int trabalho, int descanso, int maximo)
    {
        var jornada = Nova();

        Assert.Throws<DomainException>(() => jornada.Atualizar("X", trabalho, descanso, maximo, null, CriadoEm));
        Assert.Equal("6x1", jornada.Nome);
        Assert.Equal(6, jornada.MaximoDiasConsecutivos);
    }

    [Fact]
    public void Inativar_EReativar_DevemAlternarSituacaoDeFormaIdempotente()
    {
        var jornada = Nova();

        jornada.Inativar(CriadoEm.AddHours(1));
        Assert.False(jornada.Ativo);
        jornada.Inativar(CriadoEm.AddHours(2));
        Assert.False(jornada.Ativo);
        Assert.Equal(CriadoEm.AddHours(1), jornada.DataAtualizacao);

        jornada.Reativar(CriadoEm.AddHours(3));
        Assert.True(jornada.Ativo);
        Assert.False(jornada.Excluido);
    }

    [Fact]
    public void JornadaExcluida_NaoPodeSerAlteradaNemReativada()
    {
        var jornada = Nova();
        jornada.ExcluirLogicamente(Guid.NewGuid(), CriadoEm.AddHours(1));

        Assert.Throws<DomainException>(() => jornada.Atualizar("X", 6, 1, 6, null, CriadoEm.AddHours(2)));
        jornada.Reativar(CriadoEm.AddHours(3));
        Assert.False(jornada.Ativo);
    }
}
