using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Persistence;

public sealed class EmployeeModelConstraintsTests
{
    [Fact]
    public void Modelo_DeveGarantirEmailEVinculosAtivosUnicos()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var db = new AppDbContext(options, TimeProvider.System);

        var employee = db.Model.FindEntityType(typeof(Funcionario))!;
        var emailIndex = FindIndex(employee, nameof(Funcionario.Email));
        Assert.True(emailIndex.IsUnique);
        Assert.Equal("[Excluido] = 0", emailIndex.GetFilter());

        var actingUnit = db.Model.FindEntityType(typeof(FuncionarioUnidadeAtuacao))!;
        var actingUnitIndex = FindIndex(
            actingUnit,
            nameof(FuncionarioUnidadeAtuacao.FuncionarioId),
            nameof(FuncionarioUnidadeAtuacao.UnidadeHospitalarId));
        Assert.True(actingUnitIndex.IsUnique);
        Assert.Equal("[Ativo] = 1 AND [Excluido] = 0", actingUnitIndex.GetFilter());

        var sector = db.Model.FindEntityType(typeof(FuncionarioSetor))!;
        var sectorIndex = FindIndex(
            sector,
            nameof(FuncionarioSetor.FuncionarioId),
            nameof(FuncionarioSetor.SetorId));
        Assert.True(sectorIndex.IsUnique);
        Assert.Equal("[Ativo] = 1 AND [Excluido] = 0", sectorIndex.GetFilter());
    }

    private static IReadOnlyIndex FindIndex(IReadOnlyEntityType entity, params string[] properties) =>
        entity.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(properties));
}
