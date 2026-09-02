using Sistema.Gestao.Empresarial.Domain.Pessoas;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class FuncionarioTests
{
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
