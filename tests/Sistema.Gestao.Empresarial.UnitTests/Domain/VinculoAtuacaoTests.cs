using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Pessoas;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class VinculoAtuacaoTests
{
    [Fact]
    public void Encerrar_DevePreservarHistoricoSemExcluirVinculo()
    {
        var agora = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
        var vinculo = new FuncionarioUnidadeAtuacao(
            Guid.NewGuid(), 10, 20, new DateOnly(2026, 1, 1), agora);

        vinculo.Encerrar(new DateOnly(2026, 8, 31), agora.AddMinutes(1));

        Assert.False(vinculo.Ativo);
        Assert.False(vinculo.Excluido);
        Assert.Equal(new DateOnly(2026, 8, 31), vinculo.DataFim);
    }

    [Fact]
    public void Encerrar_DeveRejeitarPeriodoInvalido()
    {
        var agora = DateTimeOffset.UtcNow;
        var vinculo = new FuncionarioUnidadeAtuacao(
            Guid.NewGuid(), 10, 20, new DateOnly(2026, 8, 31), agora);

        Assert.Throws<DomainException>(() =>
            vinculo.Encerrar(new DateOnly(2026, 8, 30), agora));
    }
}
