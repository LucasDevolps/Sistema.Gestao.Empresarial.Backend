using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Persistence;

public sealed class SingleActiveSessionConstraintTests
{
    [Fact]
    public void ModeloSqlServer_DevePossuirIndiceUnicoFiltradoPorUsuarioAtivo()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=model_test;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        using var context = new AppDbContext(options, TimeProvider.System);
        var entity = context.Model.FindEntityType(typeof(UsuarioSessao))!;
        var index = entity.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1 && candidate.Properties[0].Name == nameof(UsuarioSessao.UsuarioId));

        Assert.True(index.IsUnique);
        Assert.Equal("[Ativo] = 1 AND [Revogado] = 0 AND [Excluido] = 0", index.GetFilter());
    }
}
