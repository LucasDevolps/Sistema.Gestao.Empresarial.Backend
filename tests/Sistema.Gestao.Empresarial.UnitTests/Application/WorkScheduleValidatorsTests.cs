using Sistema.Gestao.Empresarial.Application.WorkSchedules;

namespace Sistema.Gestao.Empresarial.UnitTests.Application;

public sealed class WorkScheduleValidatorsTests
{
    private readonly CreateWorkScheduleRequestValidator _create = new();
    private readonly UpdateWorkScheduleRequestValidator _update = new();
    private readonly WorkScheduleListQueryValidator _list = new();

    [Theory]
    [InlineData("6x1", 6, 1, 6)]
    [InlineData("5x2", 5, 2, 5)]
    [InlineData("Administrativo 5x2", 5, 2, 5)]
    public void RequisicaoValida_DeveSerAceitaNoCadastroENaEdicao(string name, int work, int rest, int max)
    {
        Assert.True(_create.Validate(new CreateWorkScheduleRequest(name, work, rest, max, "d")).IsValid);
        Assert.True(_update.Validate(new UpdateWorkScheduleRequest(name, work, rest, max, null)).IsValid);
    }

    [Theory]
    [InlineData("", 6, 1, 6, "Name")]
    [InlineData("  ", 6, 1, 6, "Name")]
    [InlineData("6x1", 0, 1, 6, "ConsecutiveWorkDays")]
    [InlineData("6x1", -1, 1, 6, "ConsecutiveWorkDays")]
    [InlineData("6x1", 6, 0, 6, "RestDays")]
    [InlineData("6x1", 6, -1, 6, "RestDays")]
    [InlineData("6x1", 6, 1, 0, "MaximumConsecutiveWorkDays")]
    [InlineData("6x1", 6, 1, -1, "MaximumConsecutiveWorkDays")]
    [InlineData("6x1", 6, 1, 7, "MaximumConsecutiveWorkDays")]
    [InlineData("6x1", 6, 1, 8, "MaximumConsecutiveWorkDays")]
    public void RequisicaoInvalida_DeveApontarOCampo(string name, int work, int rest, int max, string field)
    {
        var create = _create.Validate(new CreateWorkScheduleRequest(name, work, rest, max, null));
        var update = _update.Validate(new UpdateWorkScheduleRequest(name, work, rest, max, null));

        Assert.Contains(create.Errors, error => error.PropertyName == field);
        Assert.Contains(update.Errors, error => error.PropertyName == field);
    }

    [Fact]
    public void NomeEDescricaoAcimaDoLimite_DevemSerRejeitados()
    {
        var result = _create.Validate(
            new CreateWorkScheduleRequest(new string('N', 101), 6, 1, 6, new string('D', 501)));

        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        Assert.Contains(result.Errors, error => error.PropertyName == "Description");
    }

    [Fact]
    public void Listagem_DeveValidarPaginacaoEBusca()
    {
        Assert.True(_list.Validate(new WorkScheduleListQuery("6x1", null, 1, 100)).IsValid);
        Assert.False(_list.Validate(new WorkScheduleListQuery(null, null, 0, 50)).IsValid);
        Assert.False(_list.Validate(new WorkScheduleListQuery(null, null, 1, 101)).IsValid);
        Assert.False(_list.Validate(new WorkScheduleListQuery(new string('x', 101), null, 1, 50)).IsValid);
    }
}
