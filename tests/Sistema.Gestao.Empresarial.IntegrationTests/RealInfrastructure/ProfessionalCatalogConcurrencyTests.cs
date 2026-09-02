using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Infrastructure.ProfessionalCatalogs;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed class ProfessionalCatalogConcurrencyTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task NomesConcorrentes_DevemPersistirSomenteUmaProfissaoEUmCargo()
    {
        var professionName = $"Profissão concorrente {fixture.IsolationKey}";
        var positionName = $"Cargo concorrente {fixture.IsolationKey}";

        var professionAttempts = await Task.WhenAll(
            TryCreateProfessionAsync(professionName),
            TryCreateProfessionAsync(professionName));
        var positionAttempts = await Task.WhenAll(
            TryCreatePositionAsync(positionName),
            TryCreatePositionAsync(positionName));

        Assert.Single(professionAttempts, x => x.Response is not null);
        Assert.Single(professionAttempts, x => x.Error is DomainException or ProfessionalCatalogPersistenceConflictException);
        Assert.Single(positionAttempts, x => x.Response is not null);
        Assert.Single(positionAttempts, x => x.Error is DomainException or ProfessionalCatalogPersistenceConflictException);

        await using var verification = fixture.CreateDbContext();
        Assert.Equal(1, await verification.Profissoes.CountAsync(x => x.Nome == professionName));
        Assert.Equal(1, await verification.Cargos.CountAsync(x => x.Nome == positionName));
    }

    private async Task<CatalogAttempt> TryCreateProfessionAsync(string name)
    {
        try
        {
            await using var db = fixture.CreateDbContext();
            var response = await fixture.CreateProfessionalCatalogService(db).CreateProfessionAsync(
                new CreateProfessionalCatalogRequest(name, null),
                Context(),
                CancellationToken.None);
            return new CatalogAttempt(response, null);
        }
        catch (Exception exception)
        {
            return new CatalogAttempt(null, exception);
        }
    }

    private async Task<CatalogAttempt> TryCreatePositionAsync(string name)
    {
        try
        {
            await using var db = fixture.CreateDbContext();
            var response = await fixture.CreateProfessionalCatalogService(db).CreatePositionAsync(
                new CreateProfessionalCatalogRequest(name, null),
                Context(),
                CancellationToken.None);
            return new CatalogAttempt(response, null);
        }
        catch (Exception exception)
        {
            return new CatalogAttempt(null, exception);
        }
    }

    private static ProfessionalCatalogOperationContext Context() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("N"), "127.0.0.1");

    private sealed record CatalogAttempt(object? Response, Exception? Error);
}
