using FluentValidation.Results;
using Sistema.Gestao.Empresarial.Application.WorkSchedules;
using Sistema.Gestao.Empresarial.Tests.Builders;

namespace Sistema.Gestao.Empresarial.UnitTests.Application;

public sealed class WorkScheduleValidatorsTests
{
    private readonly CreateWorkScheduleRequestValidator _create = new();
    private readonly UpdateWorkScheduleRequestValidator _update = new();
    private readonly WorkScheduleListQueryValidator _list = new();

    [Fact]
    public void Jornada6x1_DeveSerAceitaNoCadastroENaEdicao()
    {
        AssertValida(new WorkScheduleRequestBuilder().WithName("6x1").WithCycle(6, 1).WithMaximum(6));
    }

    [Fact]
    public void Jornada5x2_DeveSerAceitaNoCadastroENaEdicao()
    {
        AssertValida(new WorkScheduleRequestBuilder().WithName("Administrativo 5x2").WithCycle(5, 2).WithMaximum(5));
    }

    [Fact]
    public void CicloMenorQueOMaximo_DeveSerAceito()
    {
        AssertValida(new WorkScheduleRequestBuilder().WithCycle(5, 2).WithMaximum(6));
    }

    [Fact]
    public void SemNome_DeveApontarName()
    {
        Assert.All(
            new[] { "", "  " },
            name => AssertInvalida(new WorkScheduleRequestBuilder().WithName(name), "Name"));
    }

    [Fact]
    public void DiasDeTrabalhoZeroOuNegativos_DevemApontarConsecutiveWorkDays()
    {
        Assert.All(
            new[] { 0, -1 },
            days => AssertInvalida(
                new WorkScheduleRequestBuilder().WithConsecutiveWorkDays(days), "ConsecutiveWorkDays"));
    }

    [Fact]
    public void DiasDeDescansoZeroOuNegativos_DevemApontarRestDays()
    {
        Assert.All(
            new[] { 0, -1 },
            days => AssertInvalida(new WorkScheduleRequestBuilder().WithRestDays(days), "RestDays"));
    }

    [Fact]
    public void MaximoForaDaFaixaDeUmASeis_DeveApontarMaximumConsecutiveWorkDays()
    {
        Assert.All(
            new[] { 0, -1, 7, 8 },
            maximum => AssertInvalida(
                new WorkScheduleRequestBuilder().WithMaximum(maximum), "MaximumConsecutiveWorkDays"));
    }

    [Fact]
    public void Jornada7x1_DeveTerApenasOErroDeFaixaEmConsecutiveWorkDays()
    {
        var builder = new WorkScheduleRequestBuilder().WithName("7x1").WithCycle(7, 1).WithMaximum(6);

        foreach (var result in ValidarCadastroEEdicao(builder))
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal("ConsecutiveWorkDays", error.PropertyName);
            Assert.NotEqual(WorkScheduleRules.CycleExceedsMaximumMessage, error.ErrorMessage);
        }
    }

    [Fact]
    public void Jornada7x1ComMaximoSete_DeveApontarOsDoisCampos()
    {
        var builder = new WorkScheduleRequestBuilder().WithCycle(7, 1).WithMaximum(7);

        AssertInvalida(builder, "ConsecutiveWorkDays");
        AssertInvalida(builder, "MaximumConsecutiveWorkDays");
    }

    [Fact]
    public void Jornada10x2ComMaximoSeis_DeveApontarConsecutiveWorkDays()
    {
        AssertInvalida(
            new WorkScheduleRequestBuilder().WithName("10x2").WithCycle(10, 2).WithMaximum(6),
            "ConsecutiveWorkDays");
    }

    [Fact]
    public void CicloMaiorQueOMaximo_DeveTerMensagemEspecificaUnica()
    {
        var builder = new WorkScheduleRequestBuilder().WithCycle(6, 1).WithMaximum(5);

        foreach (var result in ValidarCadastroEEdicao(builder))
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal("ConsecutiveWorkDays", error.PropertyName);
            Assert.Equal(WorkScheduleRules.CycleExceedsMaximumMessage, error.ErrorMessage);
        }
    }

    [Fact]
    public void NomeEDescricaoAcimaDoLimite_DevemSerRejeitados()
    {
        var builder = new WorkScheduleRequestBuilder()
            .WithName(new string('N', 101))
            .WithDescription(new string('D', 501));

        AssertInvalida(builder, "Name");
        AssertInvalida(builder, "Description");
    }

    [Fact]
    public void Listagem_DeveValidarPaginacaoEBusca()
    {
        Assert.True(_list.Validate(new WorkScheduleListQuery("6x1", null, 1, 100)).IsValid);
        Assert.False(_list.Validate(new WorkScheduleListQuery(null, null, 0, 50)).IsValid);
        Assert.False(_list.Validate(new WorkScheduleListQuery(null, null, 1, 101)).IsValid);
        Assert.False(_list.Validate(new WorkScheduleListQuery(new string('x', 101), null, 1, 50)).IsValid);
    }

    private ValidationResult[] ValidarCadastroEEdicao(WorkScheduleRequestBuilder builder) =>
        [_create.Validate(builder.BuildCreate()), _update.Validate(builder.BuildUpdate())];

    private void AssertValida(WorkScheduleRequestBuilder builder) =>
        Assert.All(ValidarCadastroEEdicao(builder), result => Assert.True(result.IsValid));

    private void AssertInvalida(WorkScheduleRequestBuilder builder, string field) =>
        Assert.All(
            ValidarCadastroEEdicao(builder),
            result => Assert.Contains(result.Errors, error => error.PropertyName == field));
}
