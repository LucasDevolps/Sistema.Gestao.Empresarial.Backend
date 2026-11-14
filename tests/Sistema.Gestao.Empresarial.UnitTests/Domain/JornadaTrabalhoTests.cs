using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Escalas;
using Sistema.Gestao.Empresarial.Tests.Builders;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class JornadaTrabalhoTests
{
    private static readonly DateTimeOffset CriadoEm = JornadaTrabalhoBuilder.CriadoEmPadrao;

    [Fact]
    public void Criar_6x1_DevePersistirParametros()
    {
        var jornada = new JornadaTrabalhoBuilder()
            .ComNome("6x1")
            .ComCiclo(6, 1)
            .ComMaximo(6)
            .ComDescricao("Jornada assistencial 6x1")
            .Build();

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
        var jornada = new JornadaTrabalhoBuilder().ComNome("5x2").ComCiclo(5, 2).ComMaximo(5).Build();

        Assert.Equal(5, jornada.DiasConsecutivosTrabalho);
        Assert.Equal(2, jornada.DiasDescansoCiclo);
        Assert.Equal(5, jornada.MaximoDiasConsecutivos);
        Assert.Null(jornada.Descricao);
    }

    [Fact]
    public void Criar_JornadaNaoPredefinida_DeveSerAceitaSemDependerDoNome()
    {
        var jornada = new JornadaTrabalhoBuilder()
            .ComNome("Plantão 4x2 noturno")
            .ComCiclo(4, 2)
            .ComMaximo(4)
            .Build();

        Assert.Equal(4, jornada.DiasConsecutivosTrabalho);
        Assert.Equal(2, jornada.DiasDescansoCiclo);
    }

    [Fact]
    public void Criar_DeveAplicarTrimNoNomeENaDescricao_EDescricaoVaziaViraNula()
    {
        Assert.Equal("6x1", new JornadaTrabalhoBuilder().ComNome("  6x1 ").Build().Nome);
        Assert.Equal("texto", new JornadaTrabalhoBuilder().ComDescricao("  texto ").Build().Descricao);
        Assert.Null(new JornadaTrabalhoBuilder().ComDescricao("   ").Build().Descricao);
    }

    [Fact]
    public void Criar_SemNome_DeveSerRejeitado()
    {
        Assert.All(
            new[] { "", " ", null },
            nome => Assert.Throws<DomainException>(() => new JornadaTrabalhoBuilder().ComNome(nome!).Build()));
    }

    [Fact]
    public void Criar_NomeEDescricaoAcimaDoLimite_DevemSerRejeitados()
    {
        var nomeNoLimite = new string('N', JornadaTrabalho.NomeTamanhoMaximo);

        Assert.Throws<DomainException>(() => new JornadaTrabalhoBuilder().ComNome(nomeNoLimite + "N").Build());
        Assert.Throws<DomainException>(() => new JornadaTrabalhoBuilder()
            .ComDescricao(new string('D', JornadaTrabalho.DescricaoTamanhoMaximo + 1))
            .Build());
        Assert.NotNull(new JornadaTrabalhoBuilder().ComNome(nomeNoLimite).Build());
    }

    [Fact]
    public void Criar_DiasDeTrabalhoZeroOuNegativos_DevemSerRejeitados()
    {
        Assert.All(
            new[] { 0, -1 },
            dias => Assert.Throws<DomainException>(() => new JornadaTrabalhoBuilder().ComDiasTrabalho(dias).Build()));
    }

    [Fact]
    public void Criar_DiasDeDescansoZeroOuNegativos_DevemSerRejeitados()
    {
        Assert.All(
            new[] { 0, -1 },
            dias => Assert.Throws<DomainException>(() => new JornadaTrabalhoBuilder().ComDiasDescanso(dias).Build()));
    }

    [Fact]
    public void Criar_MaximoForaDaFaixaDeUmASeis_DeveSerRejeitado()
    {
        Assert.All(
            new[] { 0, -1, 7, 8 },
            maximo => Assert.Throws<DomainException>(() => new JornadaTrabalhoBuilder().ComMaximo(maximo).Build()));
    }

    [Fact]
    public void Criar_MaximoNosLimites_DeveSerPermitido()
    {
        var minimo = new JornadaTrabalhoBuilder().ComCiclo(1, 1).ComMaximo(1).Build();
        var maximo = new JornadaTrabalhoBuilder().ComCiclo(6, 1).ComMaximo(6).Build();

        Assert.Equal(1, minimo.MaximoDiasConsecutivos);
        Assert.Equal(6, maximo.MaximoDiasConsecutivos);
    }

    [Fact]
    public void Criar_7x1_DeveSerRejeitado()
    {
        var builder = new JornadaTrabalhoBuilder().ComNome("7x1").ComCiclo(7, 1).ComMaximo(6);

        Assert.Throws<DomainException>(builder.Build);
    }

    [Fact]
    public void Criar_10x2ComMaximoSeis_DeveSerRejeitado()
    {
        var builder = new JornadaTrabalhoBuilder().ComNome("10x2").ComCiclo(10, 2).ComMaximo(6);

        Assert.Throws<DomainException>(builder.Build);
    }

    [Fact]
    public void Criar_CicloMaiorQueOMaximo_DeveSerRejeitado()
    {
        var builder = new JornadaTrabalhoBuilder().ComCiclo(6, 1).ComMaximo(5);

        Assert.Throws<DomainException>(builder.Build);
    }

    [Fact]
    public void Criar_CicloMenorQueOMaximo_DeveSerPermitido()
    {
        var jornada = new JornadaTrabalhoBuilder().ComCiclo(5, 2).ComMaximo(6).Build();

        Assert.Equal(5, jornada.DiasConsecutivosTrabalho);
        Assert.Equal(6, jornada.MaximoDiasConsecutivos);
    }

    [Fact]
    public void Criar_GuidVazio_DeveSerRejeitado()
    {
        var builder = new JornadaTrabalhoBuilder().ComGuid(Guid.Empty);

        Assert.Throws<DomainException>(builder.Build);
    }

    [Fact]
    public void Atualizar_DevePreservarGuidEAplicarNovosValores()
    {
        var jornada = new JornadaTrabalhoBuilder().Build();
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
        var jornada = new JornadaTrabalhoBuilder().Build();

        Assert.False(jornada.Atualizar(" 6x1 ", 6, 1, 6, null, CriadoEm.AddHours(1)));
        Assert.Equal(CriadoEm, jornada.DataAtualizacao);
    }

    [Fact]
    public void Atualizar_Para7x1_DeveSerRejeitadoSemAlterarEstado()
    {
        var jornada = new JornadaTrabalhoBuilder().Build();

        Assert.Throws<DomainException>(() => jornada.Atualizar("7x1", 7, 1, 6, null, CriadoEm));
        AssertJornadaPadraoInalterada(jornada);
    }

    [Fact]
    public void Atualizar_CicloMaiorQueOMaximo_DeveSerRejeitadoSemAlterarEstado()
    {
        var jornada = new JornadaTrabalhoBuilder().Build();

        Assert.Throws<DomainException>(() => jornada.Atualizar("X", 6, 1, 5, null, CriadoEm));
        AssertJornadaPadraoInalterada(jornada);
    }

    [Fact]
    public void Atualizar_MaximoSete_DeveSerRejeitadoSemAlterarEstado()
    {
        var jornada = new JornadaTrabalhoBuilder().Build();

        Assert.Throws<DomainException>(() => jornada.Atualizar("X", 6, 1, 7, null, CriadoEm));
        AssertJornadaPadraoInalterada(jornada);
    }

    [Fact]
    public void Atualizar_DiasZerados_DevemSerRejeitadosSemAlterarEstado()
    {
        var jornada = new JornadaTrabalhoBuilder().Build();

        Assert.Throws<DomainException>(() => jornada.Atualizar("X", 0, 1, 6, null, CriadoEm));
        Assert.Throws<DomainException>(() => jornada.Atualizar("X", 6, 0, 6, null, CriadoEm));
        Assert.Throws<DomainException>(() => jornada.Atualizar("X", 6, 1, 0, null, CriadoEm));
        AssertJornadaPadraoInalterada(jornada);
    }

    [Fact]
    public void Inativar_EReativar_DevemAlternarSituacaoDeFormaIdempotente()
    {
        var jornada = new JornadaTrabalhoBuilder().Build();

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
        var jornada = new JornadaTrabalhoBuilder().Build();
        jornada.ExcluirLogicamente(Guid.NewGuid(), CriadoEm.AddHours(1));

        Assert.Throws<DomainException>(() => jornada.Atualizar("X", 6, 1, 6, null, CriadoEm.AddHours(2)));
        jornada.Reativar(CriadoEm.AddHours(3));
        Assert.False(jornada.Ativo);
    }

    private static void AssertJornadaPadraoInalterada(JornadaTrabalho jornada)
    {
        Assert.Equal("6x1", jornada.Nome);
        Assert.Equal(6, jornada.DiasConsecutivosTrabalho);
        Assert.Equal(1, jornada.DiasDescansoCiclo);
        Assert.Equal(6, jornada.MaximoDiasConsecutivos);
        Assert.Equal(CriadoEm, jornada.DataAtualizacao);
    }
}
