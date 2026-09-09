using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

/// <summary>
/// Testa o contrato HTTP completo (HTTP → Controller → Service →
/// DuplicateBusinessKeyException → GlobalExceptionHandler → ProblemDetails → 409),
/// não apenas chamadas diretas ao Service.
/// </summary>
public sealed class DuplicateBusinessKeyContractTests : IClassFixture<DuplicateBusinessKeyContractApiFactory>
{
    private readonly DuplicateBusinessKeyContractApiFactory _factory;

    public DuplicateBusinessKeyContractTests(DuplicateBusinessKeyContractApiFactory factory) =>
        _factory = factory;

    // ---------- Profissão ----------

    [Fact]
    public async Task Profissao_Post_TituloInedito_Retorna201()
    {
        using var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(
            "/api/profissoes", new { name = UniqueName("Bioquímico"), description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Profissao_Post_VariacaoEquivalente_Retorna409ComProblemDetailsConsistente()
    {
        using var client = _factory.CreateApiClient();
        var name = UniqueName("Farmacêutico");

        var created = await client.PostAsJsonAsync("/api/profissoes", new { name, description = (string?)null });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Variação apenas de espaços das extremidades — equivalente para a regra de negócio.
        var conflict = await client.PostAsJsonAsync(
            "/api/profissoes", new { name = $"   {name}   ", description = (string?)null });

        await AssertDuplicateProblemAsync(conflict, expectedField: "name", submittedValue: name);
    }

    [Fact]
    public async Task Profissao_Put_MantendoProprioTitulo_Retorna200()
    {
        using var client = _factory.CreateApiClient();
        var name = UniqueName("Fisioterapeuta");

        var created = await client.PostAsJsonAsync("/api/profissoes", new { name, description = (string?)null });
        var guid = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("guid").GetGuid();

        var updated = await client.PutAsJsonAsync(
            $"/api/profissoes/{guid}", new { name, description = "Reabilitação" });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    [Fact]
    public async Task Profissao_Put_TituloDeOutroRegistro_Retorna409()
    {
        using var client = _factory.CreateApiClient();
        var first = UniqueName("Nutricionista");
        var second = UniqueName("Psicólogo");

        await client.PostAsJsonAsync("/api/profissoes", new { name = first, description = (string?)null });
        var createdSecond = await client.PostAsJsonAsync("/api/profissoes", new { name = second, description = (string?)null });
        var secondGuid = (await createdSecond.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("guid").GetGuid();

        var conflict = await client.PutAsJsonAsync(
            $"/api/profissoes/{secondGuid}", new { name = first, description = (string?)null });

        await AssertDuplicateProblemAsync(conflict, expectedField: "name", submittedValue: first);
    }

    // ---------- Cargo ----------

    [Fact]
    public async Task Cargo_Post_NomeInedito_Retorna201()
    {
        using var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(
            "/api/cargos", new { name = UniqueName("Analista"), description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Cargo_Post_Duplicado_Retorna409()
    {
        using var client = _factory.CreateApiClient();
        var name = UniqueName("Coordenador");

        await client.PostAsJsonAsync("/api/cargos", new { name, description = (string?)null });
        var conflict = await client.PostAsJsonAsync("/api/cargos", new { name = $" {name} ", description = (string?)null });

        await AssertDuplicateProblemAsync(conflict, expectedField: "name", submittedValue: name);
    }

    // ---------- Funcionário ----------

    [Fact]
    public async Task Funcionario_Post_EmailInedito_Retorna201()
    {
        var seed = await _factory.SeedEmployeeGraphAsync();
        using var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/funcionarios", EmployeePayload(seed, UniqueEmail()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Funcionario_Post_EmailDuplicadoComVariacaoDeCaixa_Retorna409()
    {
        var seed = await _factory.SeedEmployeeGraphAsync();
        using var client = _factory.CreateApiClient();
        var email = UniqueEmail();

        var created = await client.PostAsJsonAsync("/api/funcionarios", EmployeePayload(seed, email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var conflict = await client.PostAsJsonAsync(
            "/api/funcionarios", EmployeePayload(seed, email.ToUpperInvariant()));

        await AssertDuplicateProblemAsync(conflict, expectedField: "email", submittedValue: email);
    }

    [Fact]
    public async Task Funcionario_Put_MantendoProprioEmail_Retorna200()
    {
        var seed = await _factory.SeedEmployeeGraphAsync();
        using var client = _factory.CreateApiClient();
        var email = UniqueEmail();

        var created = await client.PostAsJsonAsync("/api/funcionarios", EmployeePayload(seed, email));
        var guid = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("guid").GetGuid();

        var updated = await client.PutAsJsonAsync($"/api/funcionarios/{guid}", new
        {
            name = "Nome Atualizado",
            email,
            phone = (string?)null,
            professionGuid = seed.ProfessionGuid,
            positionGuid = seed.PositionGuid,
            levelGuid = seed.LevelGuid
        });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    [Fact]
    public async Task Funcionario_Put_EmailDeOutroFuncionario_Retorna409()
    {
        var seed = await _factory.SeedEmployeeGraphAsync();
        using var client = _factory.CreateApiClient();
        var emailA = UniqueEmail();
        var emailB = UniqueEmail();

        await client.PostAsJsonAsync("/api/funcionarios", EmployeePayload(seed, emailA));
        var createdB = await client.PostAsJsonAsync("/api/funcionarios", EmployeePayload(seed, emailB));
        var guidB = (await createdB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("guid").GetGuid();

        var conflict = await client.PutAsJsonAsync($"/api/funcionarios/{guidB}", new
        {
            name = "Colisão",
            email = emailA.ToUpperInvariant(),
            phone = (string?)null,
            professionGuid = seed.ProfessionGuid,
            positionGuid = seed.PositionGuid,
            levelGuid = seed.LevelGuid
        });

        await AssertDuplicateProblemAsync(conflict, expectedField: "email", submittedValue: emailA);
    }

    // ---------- Helpers ----------

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static string UniqueEmail() => $"contrato-{Guid.NewGuid():N}@hospital.test";

    private static object EmployeePayload(DuplicateBusinessKeyContractApiFactory.EmployeeSeed seed, string email) => new
    {
        name = "Funcionário de Contrato",
        email,
        phone = "+55 11 90000-0000",
        professionGuid = seed.ProfessionGuid,
        positionGuid = seed.PositionGuid,
        levelGuid = seed.LevelGuid,
        hiringUnitGuid = seed.HiringUnitGuid,
        admissionDate = "2026-01-02",
        actingUnits = Array.Empty<object>(),
        sectors = Array.Empty<object>()
    };

    private static async Task AssertDuplicateProblemAsync(
        HttpResponseMessage response,
        string expectedField,
        string submittedValue)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var raw = await response.Content.ReadAsStringAsync();
        var problem = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(raw);
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal("Registro duplicado.", problem.GetProperty("title").GetString());
        Assert.Equal("DUPLICATE_BUSINESS_KEY", problem.GetProperty("code").GetString());
        Assert.Equal(expectedField, problem.GetProperty("field").GetString());

        Assert.True(problem.TryGetProperty("correlationId", out var correlationId));
        Assert.False(string.IsNullOrWhiteSpace(correlationId.GetString()));

        // `detail` traz uma mensagem legível, porém estável: nunca o valor informado
        // pelo usuário (título/nome/e-mail) nem detalhes de infraestrutura.
        var detail = problem.GetProperty("detail").GetString();
        Assert.False(string.IsNullOrWhiteSpace(detail));
        Assert.DoesNotContain(submittedValue, detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(submittedValue.Trim(), detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(submittedValue, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IX_", detail, StringComparison.Ordinal);
    }
}
