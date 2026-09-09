using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;
using Sistema.Gestao.Empresarial.Domain.Common;

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
        Assert.Single(professionAttempts, x => x.Error is DuplicateBusinessKeyException);
        Assert.Single(positionAttempts, x => x.Response is not null);
        Assert.Single(positionAttempts, x => x.Error is DuplicateBusinessKeyException);

        await using var verification = fixture.CreateDbContext();
        Assert.Equal(1, await verification.Profissoes.CountAsync(x => x.Nome == professionName));
        Assert.Equal(1, await verification.Cargos.CountAsync(x => x.Nome == positionName));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task VariacaoDeCaixaEEspacos_DeveSerRejeitadaPelaPreChecagem()
    {
        var professionName = $"Profissão caixa {fixture.IsolationKey}";
        var primeira = await TryCreateProfessionAsync(professionName);
        Assert.NotNull(primeira.Response);

        var maiuscula = await TryCreateProfessionAsync($"  {professionName.ToUpperInvariant()}  ");

        Assert.Null(maiuscula.Response);
        Assert.IsType<DuplicateBusinessKeyException>(maiuscula.Error);

        await using var verification = fixture.CreateDbContext();
        Assert.Equal(1, await verification.Profissoes.CountAsync(x => x.Nome == professionName));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task VariacoesDeCaixaConcorrentes_DevemPersistirSomenteUmRegistro()
    {
        var professionName = $"Profissão corrida caixa {fixture.IsolationKey}";

        var attempts = await Task.WhenAll(
            TryCreateProfessionAsync(professionName),
            TryCreateProfessionAsync(professionName.ToUpperInvariant()),
            TryCreateProfessionAsync($" {professionName} "));

        Assert.Single(attempts, x => x.Response is not null);
        Assert.Equal(2, attempts.Count(x => x.Error is DuplicateBusinessKeyException));

        await using var verification = fixture.CreateDbContext();
        Assert.Equal(
            1,
            await verification.Profissoes.CountAsync(x => x.Nome == professionName || x.Nome == professionName.ToUpperInvariant()));
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
