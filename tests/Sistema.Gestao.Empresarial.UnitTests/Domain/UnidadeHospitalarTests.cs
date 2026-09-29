using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class UnidadeHospitalarTests
{
    private static CadastroUnidadeHospitalar Valid() => new()
    {
        Nome = " Hospital Central ", Cnpj = "11.222.333/0001-81", RazaoSocial = "Hospital Ltda",
        Cep = "01001-000", Logradouro = "Praça da Sé", Numero = "10", Bairro = "Sé", Cidade = "São Paulo", Uf = "sp"
    };

    [Fact]
    public void CompleteRegistration_NormalizesAndPreservesLegacyName()
    {
        var unit = new UnidadeHospitalar(Guid.NewGuid(), 1, Valid() with { CodigoInterno = " hc ", EmailInstitucional = " CONTATO@HOSPITAL.TEST " }, DateTimeOffset.UtcNow);
        Assert.Equal("Hospital Central", unit.Nome);
        Assert.Equal("11222333000181", unit.Cnpj);
        Assert.Equal("01001000", unit.Cep);
        Assert.Equal("HC", unit.CodigoInterno);
        Assert.Equal("SP", unit.Uf);
        Assert.Equal("contato@hospital.test", unit.EmailInstitucional);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OrganizationIsMandatory(long id) => Assert.Throws<DomainException>(() => new UnidadeHospitalar(Guid.NewGuid(), id, Valid(), DateTimeOffset.UtcNow));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void NameIsMandatory(string? name) => Assert.Throws<DomainException>(() => new UnidadeHospitalar(Guid.NewGuid(), 1, name!, DateTimeOffset.UtcNow));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    [InlineData(10, 11)]
    public void InvalidBedCounts_AreRejected(int total, int icu) => Assert.Throws<DomainException>(() =>
        new UnidadeHospitalar(Guid.NewGuid(), 1, Valid() with { TotalLeitos = total, LeitosUti = icu }, DateTimeOffset.UtcNow));

    [Fact]
    public void PartialBedInformation_IsAllowedAndInactiveRegistrationDoesNotBlock()
    {
        var unit = new UnidadeHospitalar(Guid.NewGuid(), 1, Valid() with { LeitosUti = 20, SituacaoCadastral = "BAIXADA" }, DateTimeOffset.UtcNow);
        Assert.Equal(20, unit.LeitosUti);
        Assert.Null(unit.TotalLeitos);
    }

    [Fact]
    public void FailedUpdate_DoesNotPartiallyChangeEntity()
    {
        var now = DateTimeOffset.UtcNow;
        var unit = new UnidadeHospitalar(Guid.NewGuid(), 1, Valid(), now);
        Assert.Throws<DomainException>(() => unit.AtualizarCadastro(Valid() with { Nome = "Outro", LeitosUti = -1 }, now.AddMinutes(1)));
        Assert.Equal("Hospital Central", unit.Nome);
        Assert.Equal(now, unit.DataAtualizacao);
        unit.AtualizarCadastro(Valid() with { Nome = "Novo" }, now.AddMinutes(1));
        Assert.Equal("Novo", unit.Nome);
        Assert.Equal(now.AddMinutes(1), unit.DataAtualizacao);
    }

    [Fact]
    public void StatusChange_PreservesIdentityAndRegistration()
    {
        var now = DateTimeOffset.UtcNow;
        var unit = new UnidadeHospitalar(Guid.NewGuid(), 1, Valid(), now);
        var guid = unit.Guid;
        unit.Inativar(now.AddMinutes(1));
        Assert.False(unit.Ativo);
        Assert.False(unit.Excluido);
        unit.Reativar(now.AddMinutes(2));
        Assert.True(unit.Ativo);
        Assert.Equal(guid, unit.Guid);
        Assert.Equal("11222333000181", unit.Cnpj);
    }

    [Theory]
    [InlineData("11222333000181", true)]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("04.252.011/0001-10", true)]
    [InlineData("11222333000182", false)]
    [InlineData("11111111111111", false)]
    [InlineData("00000000000000", false)]
    [InlineData("1122233300018", false)]
    [InlineData("x11222333000181", false)]
    [InlineData("１1222333000181", false)]
    public void Cnpj_ChecksDigitsAndMasks(string value, bool expected) => Assert.Equal(expected, CadastroBrasileiro.CnpjValido(value));

    [Theory]
    [InlineData("01001-000", true)]
    [InlineData("01001000", true)]
    [InlineData("0100100", false)]
    [InlineData("01001A00", false)]
    public void Cep_ValidatesStructure(string value, bool expected) => Assert.Equal(expected, CadastroBrasileiro.CepValido(value));

    [Fact]
    public void OwnCnpjRequiresNumberAndLegalName_OptionalBusinessDataRemainsOptional()
    {
        Assert.Throws<DomainException>(() => (Valid() with { PossuiCnpjProprio = true, Cnpj = null }).NormalizarEValidar());
        Assert.Throws<DomainException>(() => (Valid() with { RazaoSocial = null }).NormalizarEValidar());
        var data = (Valid() with { Cnpj = null, RazaoSocial = null }).NormalizarEValidar();
        Assert.Null(data.Cnpj);
    }
}
