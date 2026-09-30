using Sistema.Gestao.Empresarial.Domain.Pessoas;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class FuncionarioTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(99)]
    public void InvalidProductivity_RejectsCreationAndUpdateWithoutMutation(int productivity)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<Sistema.Gestao.Empresarial.Domain.Common.DomainException>(() => new Funcionario(
            Guid.NewGuid(), "Maria", "maria@test.com", null, 1, 2, 3, 4, new DateOnly(2026, 1, 1), now, productivity));
        var employee = new Funcionario(Guid.NewGuid(), "Maria", "maria@test.com", null,
            1, 2, 3, 4, new DateOnly(2026, 1, 1), now, 2, true);
        Assert.Throws<Sistema.Gestao.Empresarial.Domain.Common.DomainException>(() => employee.AtualizarDados(
            "Changed", "changed@test.com", null, 1, 2, 3, now.AddMinutes(1), productivity, false));
        Assert.Equal("Maria", employee.Nome);
        Assert.Equal(2, employee.Produtividade);
        Assert.True(employee.ParticipaDaEscala);
        Assert.Equal(now, employee.DataAtualizacao);
    }

    [Fact]
    public void ScaleParameters_UpdateTimestampAndRemainIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var employee = new Funcionario(Guid.NewGuid(), "Maria", "maria@test.com", null,
            1, 2, 3, 4, new DateOnly(2026, 1, 1), now);
        Assert.True(employee.AtualizarDados("Maria", "maria@test.com", null, 1, 2, 3, now.AddMinutes(1), 1, true));
        Assert.Equal(now.AddMinutes(1), employee.DataAtualizacao);
        Assert.False(employee.AtualizarDados("Maria", "maria@test.com", null, 1, 2, 3, now.AddMinutes(2), 1, true));
        Assert.Equal(now.AddMinutes(1), employee.DataAtualizacao);
    }

    [Fact]
    public void AtualizarDados_DevePreservarOrigemContratualEDataDeAdmissao()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var admissionDate = new DateOnly(2026, 1, 2);
        var employee = new Funcionario(
            Guid.NewGuid(), "Maria", "MARIA@HOSPITAL.TEST", null,
            10, 20, 30, 40, admissionDate, now);

        var changed = employee.AtualizarDados(
            "Maria da Silva", "nova@hospital.test", "  +55 11 99999-0000  ",
            11, 21, 31, now.AddMinutes(1));

        Assert.True(changed);
        Assert.Equal(40, employee.UnidadeContratacaoId);
        Assert.Equal(admissionDate, employee.DataAdmissao);
        Assert.Equal("+55 11 99999-0000", employee.Telefone);
        Assert.Equal("nova@hospital.test", employee.Email);
    }

    [Fact]
    public void AtualizarDadosSemMudancas_DeveSerIdempotente()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var employee = new Funcionario(
            Guid.NewGuid(), "Maria", "maria@hospital.test", null,
            10, 20, 30, 40, new DateOnly(2026, 1, 2), now);

        var changed = employee.AtualizarDados(
            " Maria ", "MARIA@HOSPITAL.TEST", " ",
            10, 20, 30, now.AddMinutes(1));

        Assert.False(changed);
        Assert.Equal(now, employee.DataAtualizacao);
    }
}
