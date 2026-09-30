using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sistema.Gestao.Empresarial.Application.Authorization;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

/// <summary>
/// Contrato HTTP de <c>/api/jornadas-trabalho</c> (issue #51): autenticação,
/// autorização por permissão específica, status codes, ProblemDetails e ciclo
/// cadastro → consulta → listagem → edição → inativação → reativação, pelo pipeline
/// real da API (sem endpoint de exclusão).
/// </summary>
public sealed class WorkSchedulesApiTests : IClassFixture<ProfessionalLevelsApiFactory>
{
    private const string Route = "/api/jornadas-trabalho";
    private readonly ProfessionalLevelsApiFactory _factory;

    public WorkSchedulesApiTests(ProfessionalLevelsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SemAutenticacao_TodasAsOperacoesRetornam401()
    {
        using var client = _factory.CreateApiClient(null);
        var guid = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Route}/{guid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Route, Payload("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"{Route}/{guid}", Payload("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PatchAsJsonAsync($"{Route}/{guid}/status", new { active = false })).StatusCode);
    }

    [Fact]
    public async Task SemPermissao_TodasAsOperacoesRetornam403()
    {
        using var client = _factory.CreateApiClient(_factory.GrantPermissions());
        var guid = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Route}/{guid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Route, Payload("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Route}/{guid}", Payload("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PatchAsJsonAsync($"{Route}/{guid}/status", new { active = false })).StatusCode);
    }

    [Fact]
    public async Task SomenteVisualizar_NaoPermiteCriarNemEditarNemMudarStatus()
    {
        using var admin = AdminClient();
        var schedule = await CreateAsync(admin, "V " + Suffix());
        using var reader = _factory.CreateApiClient(_factory.GrantPermissions(PermissionCodes.ViewWorkSchedules));

        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"{Route}/{schedule}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(Route, Payload("X " + Suffix()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync($"{Route}/{schedule}", Payload("Y"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reader.PatchAsJsonAsync($"{Route}/{schedule}/status", new { active = false })).StatusCode);
    }

    [Fact]
    public async Task NaoExisteEndpointDeExclusaoFisica()
    {
        using var admin = AdminClient();
        var schedule = await CreateAsync(admin, "D " + Suffix());

        var response = await admin.DeleteAsync($"{Route}/{schedule}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{Route}/{schedule}")).StatusCode);
    }

    [Fact]
    public async Task Cadastrar_Valido_Retorna201ComLocationEContratoPublicoSemIdInterno()
    {
        using var client = AdminClient();
        var name = "6x1 " + Suffix();

        var response = await client.PostAsJsonAsync(Route, Payload($"  {name} ", 6, 1, 6, "Assistencial"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var guid = body.GetProperty("guid").GetGuid();
        Assert.Equal($"/api/jornadas-trabalho/{guid}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal(6, body.GetProperty("consecutiveWorkDays").GetInt32());
        Assert.Equal(1, body.GetProperty("restDays").GetInt32());
        Assert.Equal(6, body.GetProperty("maximumConsecutiveWorkDays").GetInt32());
        Assert.Equal("Assistencial", body.GetProperty("description").GetString());
        Assert.True(body.GetProperty("active").GetBoolean());
        Assert.False(body.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Cadastrar_5x2_Retorna201()
    {
        using var client = AdminClient();

        var response = await client.PostAsJsonAsync(Route, Payload("5x2 " + Suffix(), 5, 2, 5));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 1, 6, "ConsecutiveWorkDays")]
    [InlineData(-1, 1, 6, "ConsecutiveWorkDays")]
    [InlineData(6, 0, 6, "RestDays")]
    [InlineData(6, -1, 6, "RestDays")]
    [InlineData(6, 1, 0, "MaximumConsecutiveWorkDays")]
    [InlineData(6, 1, -1, "MaximumConsecutiveWorkDays")]
    [InlineData(6, 1, 7, "MaximumConsecutiveWorkDays")]
    [InlineData(6, 1, 8, "MaximumConsecutiveWorkDays")]
    public async Task CadastrarEEditar_ParametrosInvalidos_Retornam400ComErroNoCampo(
        int work, int rest, int max, string field)
    {
        using var client = AdminClient();
        var guid = await CreateAsync(client, "Base " + Suffix());

        var create = await client.PostAsJsonAsync(Route, Payload("Inv " + Suffix(), work, rest, max));
        var update = await client.PutAsJsonAsync($"{Route}/{guid}", Payload("Inv " + Suffix(), work, rest, max));

        foreach (var response in new[] { create, update })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            Assert.True(errors.TryGetProperty(field, out _));
        }
    }

    [Fact]
    public async Task Cadastrar_NomeVazio_Retorna400()
    {
        using var client = AdminClient();

        var response = await client.PostAsJsonAsync(Route, Payload("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cadastrar_DuplicadoComVariacaoDeEspacos_Retorna409ComFieldNameSemVazarDetalhes()
    {
        using var client = AdminClient();
        var name = "Dup " + Suffix();
        await CreateAsync(client, name);

        var response = await client.PostAsJsonAsync(Route, Payload($"  {name}  "));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("DUPLICATE_BUSINESS_KEY", problem.GetProperty("code").GetString());
        Assert.Equal("name", problem.GetProperty("field").GetString());
        Assert.DoesNotContain(name, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IX_", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consultar_Inexistente_Retorna404()
    {
        using var client = AdminClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", Payload("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PatchAsJsonAsync($"{Route}/{Guid.NewGuid()}/status", new { active = false })).StatusCode);
    }

    [Fact]
    public async Task Editar_DeveManterGuidECriarConflitoComOutraJornada()
    {
        using var client = AdminClient();
        var suffix = Suffix();
        var first = await CreateAsync(client, "A " + suffix);
        await CreateAsync(client, "B " + suffix);

        var ok = await client.PutAsJsonAsync($"{Route}/{first}", Payload("Adm 5x2 " + suffix, 5, 2, 5, "Adm"));
        var conflict = await client.PutAsJsonAsync($"{Route}/{first}", Payload("B " + suffix));

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first, body.GetProperty("guid").GetGuid());
        Assert.Equal(5, body.GetProperty("consecutiveWorkDays").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task InativarConsultarReativar_DevePreservarRegistroEFiltrarNaListagem()
    {
        using var client = AdminClient();
        var name = "Ciclo " + Suffix();
        var guid = await CreateAsync(client, name);

        var inactive = await client.PatchAsJsonAsync($"{Route}/{guid}/status", new { active = false });
        var single = await client.GetFromJsonAsync<JsonElement>($"{Route}/{guid}");
        var activeList = await client.GetFromJsonAsync<JsonElement>($"{Route}?search={name}&active=true");
        var inactiveList = await client.GetFromJsonAsync<JsonElement>($"{Route}?search={name}&active=false");
        var allList = await client.GetFromJsonAsync<JsonElement>($"{Route}?search={name}");
        var reactivated = await client.PatchAsJsonAsync($"{Route}/{guid}/status", new { active = true });

        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
        Assert.False(single.GetProperty("active").GetBoolean());
        Assert.Equal(0, activeList.GetProperty("total").GetInt32());
        Assert.Equal(1, inactiveList.GetProperty("total").GetInt32());
        Assert.Equal(1, allList.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.True((await reactivated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task Listar_DeveFiltrarPorNomeEPaginar()
    {
        using var client = AdminClient();
        var tag = Suffix();
        foreach (var index in Enumerable.Range(1, 3))
        {
            await CreateAsync(client, $"Pag{tag} {index}");
        }

        var page = await client.GetFromJsonAsync<JsonElement>($"{Route}?search=Pag{tag}&page=2&pageSize=2");

        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Route}?pageSize=101")).StatusCode);
    }

    private HttpClient AdminClient() =>
        _factory.CreateApiClient(_factory.GrantPermissions(
            PermissionCodes.ViewWorkSchedules,
            PermissionCodes.CreateWorkSchedules,
            PermissionCodes.EditWorkSchedules));

    private static object Payload(
        string name, int work = 6, int rest = 1, int max = 6, string? description = null) =>
        new
        {
            name,
            consecutiveWorkDays = work,
            restDays = rest,
            maximumConsecutiveWorkDays = max,
            description
        };

    private static string Suffix() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task<Guid> CreateAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(Route, Payload(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("guid").GetGuid();
    }
}
