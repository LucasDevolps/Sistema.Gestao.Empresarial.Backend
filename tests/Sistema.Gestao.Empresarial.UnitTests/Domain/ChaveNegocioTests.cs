using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class ChaveNegocioTests
{
    [Theory]
    [InlineData("Farmacêutico", "Farmacêutico")]
    [InlineData("  Farmacêutico", "Farmacêutico")]
    [InlineData("Farmacêutico   ", "Farmacêutico")]
    [InlineData("  Farmacêutico  ", "Farmacêutico")]
    [InlineData("\tFarmacêutico\r\n", "Farmacêutico")]
    public void Normalizar_DeveRemoverApenasEspacosDasExtremidades(string entrada, string esperado)
    {
        Assert.Equal(esperado, ChaveNegocio.Normalizar(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_ValorVazioOuNulo_DeveRetornarStringVazia(string? entrada)
    {
        Assert.Equal(string.Empty, ChaveNegocio.Normalizar(entrada));
    }

    [Fact]
    public void Normalizar_NaoDeveAlterarCaixaNemAcentuacao()
    {
        Assert.Equal("FARMACÊUTICO", ChaveNegocio.Normalizar("  FARMACÊUTICO  "));
        Assert.Equal("Medico", ChaveNegocio.Normalizar("Medico"));
        Assert.NotEqual(ChaveNegocio.Normalizar("Médico"), ChaveNegocio.Normalizar("Medico"));
    }
}
