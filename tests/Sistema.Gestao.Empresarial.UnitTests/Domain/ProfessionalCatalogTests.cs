using Sistema.Gestao.Empresarial.Domain.Pessoas;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class ProfessionalCatalogTests
{
    [Fact]
    public void AtualizarProfissao_DeveNormalizarValoresESerIdempotente()
    {
        var createdAt = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(1);
        var profession = new Profissao(Guid.NewGuid(), "Farmacêutico", null, createdAt);

        Assert.True(profession.Atualizar(" Farmacêutico Clínico ", " Assistência hospitalar ", updatedAt));
        Assert.Equal("Farmacêutico Clínico", profession.Nome);
        Assert.Equal("Assistência hospitalar", profession.Descricao);
        Assert.Equal(updatedAt, profession.DataAtualizacao);

        Assert.False(profession.Atualizar(
            "Farmacêutico Clínico", "Assistência hospitalar", updatedAt.AddMinutes(1)));
        Assert.Equal(updatedAt, profession.DataAtualizacao);
    }

    [Fact]
    public void AtualizarCargo_DeveNormalizarValoresESerIdempotente()
    {
        var createdAt = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(1);
        var position = new Cargo(Guid.NewGuid(), "Farmacêutico", null, createdAt);

        Assert.True(position.Atualizar(" Farmacêutico Clínico ", " Farmácia clínica ", updatedAt));
        Assert.Equal("Farmacêutico Clínico", position.Nome);
        Assert.Equal("Farmácia clínica", position.Descricao);
        Assert.Equal(updatedAt, position.DataAtualizacao);

        Assert.False(position.Atualizar(
            "Farmacêutico Clínico", "Farmácia clínica", updatedAt.AddMinutes(1)));
        Assert.Equal(updatedAt, position.DataAtualizacao);
    }
}
