using Sistema.Gestao.Empresarial.Domain.Organizacoes;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class ExclusaoLogicaTests
{
    [Fact]
    public void ExcluirLogicamente_DeveSerIdempotenteEPreservarPrimeiroResponsavel()
    {
        var criadaEm = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
        var primeiroAtor = Guid.NewGuid();
        var primeiraExclusao = criadaEm.AddMinutes(1);
        var organizacao = new Organizacao(Guid.NewGuid(), "Hospitalar", criadaEm);

        organizacao.ExcluirLogicamente(primeiroAtor, primeiraExclusao);
        organizacao.ExcluirLogicamente(Guid.NewGuid(), criadaEm.AddMinutes(2));

        Assert.True(organizacao.Excluido);
        Assert.False(organizacao.Ativo);
        Assert.Equal(primeiroAtor, organizacao.ExcluidoPor);
        Assert.Equal(primeiraExclusao, organizacao.ExcluidoEm);
    }
}
