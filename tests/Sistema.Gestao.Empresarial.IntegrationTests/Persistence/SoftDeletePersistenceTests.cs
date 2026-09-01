using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Persistence;

public sealed class SoftDeletePersistenceTests
{
    [Fact]
    public async Task QueryFilter_DeveOcultarRegistroExcluidoLogicamente()
    {
        await using var context = CreateContext();
        var organizacao = new Organizacao(Guid.NewGuid(), "Organização teste", DateTimeOffset.UtcNow);
        context.Organizacoes.Add(organizacao);
        await context.SaveChangesAsync();

        organizacao.ExcluirLogicamente(Guid.NewGuid(), DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Empty(await context.Organizacoes.ToListAsync());
        Assert.Single(await context.Organizacoes.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_DeveBloquearEstadoDeleted()
    {
        await using var context = CreateContext();
        var organizacao = new Organizacao(Guid.NewGuid(), "Organização teste", DateTimeOffset.UtcNow);
        context.Organizacoes.Add(organizacao);
        await context.SaveChangesAsync();

        context.Entry(organizacao).State = EntityState.Deleted;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("Exclusão física é proibida", exception.Message);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options, TimeProvider.System);
    }
}
