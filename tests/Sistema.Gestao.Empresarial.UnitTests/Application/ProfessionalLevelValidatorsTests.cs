using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;

namespace Sistema.Gestao.Empresarial.UnitTests.Application;

public sealed class ProfessionalLevelValidatorsTests
{
    private readonly CreateProfessionalLevelRequestValidator _create = new();
    private readonly UpdateProfessionalLevelRequestValidator _update = new();

    [Fact]
    public void RequisicaoValida_DeveSerAceitaNoCadastroENaEdicao()
    {
        Assert.True(_create.Validate(new CreateProfessionalLevelRequest("JR", "Júnior", 1)).IsValid);
        Assert.True(_update.Validate(new UpdateProfessionalLevelRequest("JR", "Júnior", 9999)).IsValid);
    }

    [Theory]
    [InlineData("", "Júnior", 1, "Code")]
    [InlineData("ABCDEFGHIJK", "Júnior", 1, "Code")]
    [InlineData("JR", "", 1, "Name")]
    [InlineData("JR", null, 1, "Name")]
    [InlineData("JR", "Júnior", 0, "Order")]
    [InlineData("JR", "Júnior", 10_000, "Order")]
    public void RequisicaoInvalida_DeveApontarOCampo(string code, string? name, int order, string field)
    {
        var create = _create.Validate(new CreateProfessionalLevelRequest(code, name!, order));
        var update = _update.Validate(new UpdateProfessionalLevelRequest(code, name!, order));

        Assert.Contains(create.Errors, error => error.PropertyName == field);
        Assert.Contains(update.Errors, error => error.PropertyName == field);
    }

    [Fact]
    public void NomeAcimaDe80Caracteres_DeveSerRejeitado()
    {
        var result = _create.Validate(new CreateProfessionalLevelRequest("JR", new string('N', 81), 1));

        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }
}
