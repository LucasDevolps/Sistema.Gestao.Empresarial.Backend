using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;
using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

/// <summary>
/// Unicidade de código/nome de nível profissional e a invariável "nível excluído não
/// tem funcionários" sob concorrência real no SQL Server (índices únicos filtrados,
/// collation <c>Latin1_General_CI_AS</c> e <c>sp_getapplock</c>).
/// </summary>
[Collection(RealInfrastructureCollection.Name)]
public sealed class ProfessionalLevelConcurrencyTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task CodigosConcorrentes_DevemPersistirSomenteUmNivel()
    {
        var code = UniqueCode();

        var attempts = await Task.WhenAll(
            TryCreateAsync(code, $"Nível A {fixture.IsolationKey}"),
            TryCreateAsync(code.ToLowerInvariant(), $"Nível B {fixture.IsolationKey}"),
            TryCreateAsync($" {code} ", $"Nível C {fixture.IsolationKey}"));

        Assert.Single(attempts, x => x.Response is not null);
        Assert.All(
            attempts.Where(x => x.Response is null),
            x => Assert.Equal("code", Assert.IsType<DuplicateBusinessKeyException>(x.Error).Field));

        await using var verification = fixture.CreateDbContext();
        Assert.Equal(1, await verification.NiveisProfissionais.CountAsync(x => x.Codigo == code));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task NomesConcorrentesComVariacaoDeCaixa_DevemPersistirSomenteUmNivel()
    {
        var name = $"Sênior concorrente {fixture.IsolationKey}";

        var attempts = await Task.WhenAll(
            TryCreateAsync(UniqueCode(), name),
            TryCreateAsync(UniqueCode(), name.ToUpperInvariant()),
            TryCreateAsync(UniqueCode(), $"  {name.ToLowerInvariant()}  "));

        Assert.Single(attempts, x => x.Response is not null);
        Assert.All(
            attempts.Where(x => x.Response is null),
            x => Assert.Equal("name", Assert.IsType<DuplicateBusinessKeyException>(x.Error).Field));

        await using var verification = fixture.CreateDbContext();
        Assert.Equal(1, await verification.NiveisProfissionais.CountAsync(x => x.Nome == name));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task NomeComVariacaoDeCaixa_DeveSerRejeitadoPelaPreChecagemNoCadastroENaEdicao()
    {
        var name = $"Pleno caixa {fixture.IsolationKey}";
        var first = await TryCreateAsync(UniqueCode(), name);
        var other = await TryCreateAsync(UniqueCode(), $"Outro {fixture.IsolationKey}");
        Assert.NotNull(first.Response);
        Assert.NotNull(other.Response);

        var duplicateOnCreate = await TryCreateAsync(UniqueCode(), name.ToUpperInvariant());
        Exception? duplicateOnUpdate;
        await using (var db = fixture.CreateDbContext())
        {
            duplicateOnUpdate = await Record.ExceptionAsync(() =>
                fixture.CreateProfessionalCatalogService(db).UpdateLevelAsync(
                    other.Response!.Guid,
                    new UpdateProfessionalLevelRequest(other.Response.Code, name.ToLowerInvariant(), 1),
                    Context(),
                    CancellationToken.None));
        }

        Assert.Equal("name", Assert.IsType<DuplicateBusinessKeyException>(duplicateOnCreate.Error).Field);
        Assert.Equal("name", Assert.IsType<DuplicateBusinessKeyException>(duplicateOnUpdate).Field);
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task ExclusaoConcorrenteComVinculoDeFuncionario_NuncaDeixaFuncionarioEmNivelExcluido()
    {
        for (var round = 0; round < 5; round++)
        {
            var level = await TryCreateAsync(UniqueCode(), $"Corrida {round} {fixture.IsolationKey}");
            var levelGuid = level.Response!.Guid;

            var outcomes = await Task.WhenAll(
                CaptureAsync(async () =>
                {
                    await using var db = fixture.CreateDbContext();
                    await fixture.CreateProfessionalCatalogService(db)
                        .DeleteLevelAsync(levelGuid, Context(), CancellationToken.None);
                }),
                CaptureAsync(async () =>
                {
                    await using var db = fixture.CreateDbContext();
                    var email = $"corrida-{Guid.NewGuid():N}@hospital.test";
                    var request = fixture.CreateEmployeeRequest(email) with { LevelGuid = levelGuid };
                    await fixture.CreateEmployeeService(db).CreateAsync(
                        request, fixture.CreateOperationContext(Guid.NewGuid()), CancellationToken.None);
                }));

            // Exatamente uma das operações vence; a perdedora recebe erro de negócio.
            Assert.Single(outcomes, x => x is null);
            Assert.IsType<DomainException>(Assert.Single(outcomes, x => x is not null));

            await using var verification = fixture.CreateDbContext();
            var stored = await verification.NiveisProfissionais.IgnoreQueryFilters()
                .SingleAsync(x => x.Guid == levelGuid);
            var employees = await verification.Funcionarios.CountAsync(x => x.NivelId == stored.Id);
            Assert.False(stored.Excluido && employees > 0);
        }
    }

    private async Task<LevelAttempt> TryCreateAsync(string code, string name)
    {
        try
        {
            await using var db = fixture.CreateDbContext();
            var response = await fixture.CreateProfessionalCatalogService(db).CreateLevelAsync(
                new CreateProfessionalLevelRequest(code, name, 1),
                Context(),
                CancellationToken.None);
            return new LevelAttempt(response, null);
        }
        catch (Exception exception)
        {
            return new LevelAttempt(null, exception);
        }
    }

    private static async Task<Exception?> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    // Códigos têm no máximo 10 caracteres: prefixo + 8 hex aleatórios evita colisão
    // entre testes que compartilham o banco do fixture.
    private static string UniqueCode() => "L" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static ProfessionalCatalogOperationContext Context() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("N"), "127.0.0.1");

    private sealed record LevelAttempt(ProfessionalLevelResponse? Response, Exception? Error);
}
