using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Pessoas;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class NivelProfissionalTests
{
    private static readonly DateTimeOffset CriadoEm = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_DeveNormalizarCodigoEmMaiusculasENomeSemEspacos()
    {
        var nivel = new NivelProfissional(Guid.NewGuid(), "  coord ", "  Coordenação  ", 5, CriadoEm);

        Assert.Equal("COORD", nivel.Codigo);
        Assert.Equal("Coordenação", nivel.Nome);
        Assert.Equal(5, nivel.Ordem);
        Assert.True(nivel.Ativo);
        Assert.False(nivel.Excluido);
    }

    [Theory]
    [InlineData("", "Júnior")]
    [InlineData("   ", "Júnior")]
    [InlineData("JR", "")]
    [InlineData("JR", "   ")]
    [InlineData("CODIGOLONGO", "Júnior")]
    public void Criar_ComCodigoOuNomeInvalido_DeveLancarDomainException(string codigo, string nome)
    {
        Assert.Throws<DomainException>(() => new NivelProfissional(Guid.NewGuid(), codigo, nome, 1, CriadoEm));
    }

    [Fact]
    public void Criar_ComNomeAcimaDoLimite_DeveLancarDomainException()
    {
        var nome = new string('N', NivelProfissional.NomeTamanhoMaximo + 1);

        Assert.Throws<DomainException>(() => new NivelProfissional(Guid.NewGuid(), "N", nome, 1, CriadoEm));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(NivelProfissional.OrdemMaxima + 1)]
    public void Criar_ComOrdemForaDoIntervalo_DeveLancarDomainException(int ordem)
    {
        Assert.Throws<DomainException>(() => new NivelProfissional(Guid.NewGuid(), "JR", "Júnior", ordem, CriadoEm));
    }

    [Theory]
    [InlineData(NivelProfissional.OrdemMinima)]
    [InlineData(NivelProfissional.OrdemMaxima)]
    public void Criar_ComOrdemNosLimites_DeveSerAceita(int ordem)
    {
        var nivel = new NivelProfissional(Guid.NewGuid(), "JR", "Júnior", ordem, CriadoEm);

        Assert.Equal(ordem, nivel.Ordem);
    }

    [Fact]
    public void Atualizar_DeveNormalizarPreservarIdentidadeESerIdempotente()
    {
        var guid = Guid.NewGuid();
        var nivel = new NivelProfissional(guid, "SR", "Sênior", 3, CriadoEm);
        var atualizadoEm = CriadoEm.AddMinutes(1);

        Assert.True(nivel.Atualizar(" sr2 ", " Sênior II ", 4, atualizadoEm));
        Assert.Equal(guid, nivel.Guid);
        Assert.Equal("SR2", nivel.Codigo);
        Assert.Equal("Sênior II", nivel.Nome);
        Assert.Equal(4, nivel.Ordem);
        Assert.Equal(atualizadoEm, nivel.DataAtualizacao);

        Assert.False(nivel.Atualizar("sr2", "Sênior II", 4, atualizadoEm.AddMinutes(1)));
        Assert.Equal(atualizadoEm, nivel.DataAtualizacao);
    }

    [Fact]
    public void Atualizar_ComOrdemInvalida_NaoDeveAlterarNada()
    {
        var nivel = new NivelProfissional(Guid.NewGuid(), "SR", "Sênior", 3, CriadoEm);

        Assert.Throws<DomainException>(() => nivel.Atualizar("SR", "Sênior", 0, CriadoEm.AddMinutes(1)));
        Assert.Equal(3, nivel.Ordem);
        Assert.Equal(CriadoEm, nivel.DataAtualizacao);
    }

    [Fact]
    public void Atualizar_NivelExcluido_DeveSerRejeitado()
    {
        var nivel = new NivelProfissional(Guid.NewGuid(), "SR", "Sênior", 3, CriadoEm);
        nivel.ExcluirLogicamente(Guid.NewGuid(), CriadoEm.AddMinutes(1));

        Assert.Throws<DomainException>(() => nivel.Atualizar("SR", "Sênior II", 3, CriadoEm.AddMinutes(2)));
        Assert.Equal("Sênior", nivel.Nome);
    }

    [Fact]
    public void ExcluirLogicamente_DeveRegistrarResponsavelEDesativar()
    {
        var nivel = new NivelProfissional(Guid.NewGuid(), "SR", "Sênior", 3, CriadoEm);
        var ator = Guid.NewGuid();
        var excluidoEm = CriadoEm.AddMinutes(1);

        nivel.ExcluirLogicamente(ator, excluidoEm);

        Assert.True(nivel.Excluido);
        Assert.False(nivel.Ativo);
        Assert.Equal(ator, nivel.ExcluidoPor);
        Assert.Equal(excluidoEm, nivel.ExcluidoEm);
    }
}
