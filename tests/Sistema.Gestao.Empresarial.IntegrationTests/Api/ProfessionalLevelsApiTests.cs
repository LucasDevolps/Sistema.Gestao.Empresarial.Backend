using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

/// <summary>
/// Contrato HTTP de <c>/api/niveis-profissionais</c> (issue #47): autenticação,
/// autorização por permissão específica, status codes, ProblemDetails e exclusão
/// lógica — de ponta a ponta pelo pipeline real da API.
/// </summary>
public sealed class ProfessionalLevelsApiTests : IClassFixture<ProfessionalLevelsApiFactory>
{
    private const string Route = "/api/niveis-profissionais";
    private readonly ProfessionalLevelsApiFactory _factory;

    public ProfessionalLevelsApiTests(ProfessionalLevelsApiFactory factory) => _factory = factory;

    // ---------- Autenticação e autorização ----------

    [Fact]
    public async Task SemAutenticacao_TodasAsOperacoesRetornam401()
    {
        using var client = _factory.CreateApiClient(null);
        var guid = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Route}/{guid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Route, Payload("X", "Y", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"{Route}/{guid}", Payload("X", "Y", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"{Route}/{guid}/excluir", null)).StatusCode);
    }

    [Fact]
    public async Task SemPermissao_TodasAsOperacoesRetornam403()
    {
        using var client = _factory.CreateApiClient(_factory.GrantPermissions());
        var guid = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Route}/{guid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Route, Payload("X", "Y", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Route}/{guid}", Payload("X", "Y", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Route}/{guid}/excluir", null)).StatusCode);
    }

    [Fact]
    public async Task SomenteVisualizar_NaoPermiteCriarEditarNemExcluir()
    {
        using var admin = AdminClient();
        var level = await CreateAsync(admin, "V" + Suffix(), "Visualização " + Suffix(), 1);
        using var reader = _factory.CreateApiClient(
            _factory.GrantPermissions(PermissionCodes.ViewProfessionalLevels));

        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"{Route}/{level.Guid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(Route, Payload("X", "Y", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reader.PutAsJsonAsync($"{Route}/{level.Guid}", Payload(level.Code, "Outro", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsync($"{Route}/{level.Guid}/excluir", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"{Route}/{level.Guid}")).StatusCode);
    }

    [Fact]
    public async Task CriarSemVisualizar_DeveSerPermitidoPelaPermissaoCriar()
    {
        using var creator = _factory.CreateApiClient(
            _factory.GrantPermissions(PermissionCodes.CreateProfessionalLevels));

        var response = await creator.PostAsJsonAsync(Route, Payload("C" + Suffix(), "Criador " + Suffix(), 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------- CRUD ----------

    [Fact]
    public async Task Cadastrar_Valido_Retorna201ComLocationEContratoPublicoSemIdInterno()
    {
        using var client = AdminClient();
        var code = "E" + Suffix();

        var response = await client.PostAsJsonAsync(Route, Payload($" {code.ToLowerInvariant()} ", "  Especialista " + code + " ", 4));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var guid = body.GetProperty("guid").GetGuid();
        Assert.Equal($"/api/niveis-profissionais/{guid}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal("Especialista " + code, body.GetProperty("name").GetString());
        Assert.Equal(4, body.GetProperty("order").GetInt32());
        Assert.False(body.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Cadastrar_Invalido_Retorna400ComErrosPorCampo()
    {
        using var client = AdminClient();

        var response = await client.PostAsJsonAsync(Route, Payload("", new string('N', 81), 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Code", out _));
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Order", out _));
    }

    [Fact]
    public async Task Cadastrar_CodigoMaiorQueOLimite_Retorna400()
    {
        using var client = AdminClient();

        var response = await client.PostAsJsonAsync(Route, Payload(new string('C', 11), "Nome " + Suffix(), 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("name")]
    public async Task Cadastrar_Duplicado_Retorna409ComCodeEFieldSemVazarDetalhes(string field)
    {
        using var client = AdminClient();
        var code = "D" + Suffix();
        var name = "Duplicado " + Suffix();
        await CreateAsync(client, code, name, 1);

        var payload = field == "code"
            ? Payload(code.ToLowerInvariant(), "Outro " + Suffix(), 1)
            : Payload("O" + Suffix(), $"  {name}  ", 1);
        var response = await client.PostAsJsonAsync(Route, payload);

        await AssertDuplicateAsync(response, field, field == "code" ? code : name);
    }

    [Fact]
    public async Task Listar_RetornaEnvelopePaginadoOrdenadoEFiltradoPelaBusca()
    {
        using var client = AdminClient();
        var marker = Suffix();
        await CreateAsync(client, "B" + marker, $"Busca {marker} B", 2);
        await CreateAsync(client, "A" + marker, $"Busca {marker} A", 2);
        await CreateAsync(client, "C" + marker, $"Busca {marker} C", 1);

        var response = await client.GetAsync($"{Route}?search={marker}&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("total").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(10, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(
            ["C" + marker, "A" + marker, "B" + marker],
            body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Listar_PaginacaoInvalida_Retorna400()
    {
        using var client = AdminClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Route}?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Route}?page=0")).StatusCode);
    }

    [Fact]
    public async Task Consultar_Inexistente_Retorna404()
    {
        using var client = AdminClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Editar_ValidoEParaChaveDeOutroRegistro()
    {
        using var client = AdminClient();
        var first = await CreateAsync(client, "F" + Suffix(), "Primeiro " + Suffix(), 1);
        var second = await CreateAsync(client, "S" + Suffix(), "Segundo " + Suffix(), 2);

        var updated = await client.PutAsJsonAsync($"{Route}/{second.Guid}", Payload(second.Code, second.Name + " II", 3));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(second.Guid, body.GetProperty("guid").GetGuid());
        Assert.Equal(3, body.GetProperty("order").GetInt32());

        var conflict = await client.PutAsJsonAsync($"{Route}/{second.Guid}", Payload(first.Code, "Qualquer " + Suffix(), 3));
        await AssertDuplicateAsync(conflict, "code", first.Code);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", Payload("X", "Y", 1))).StatusCode);
    }

    [Fact]
    public async Task Excluir_Retorna204EDepoisOTratamentoDeInexistente()
    {
        using var client = AdminClient();
        var level = await CreateAsync(client, "X" + Suffix(), "Excluível " + Suffix(), 1);

        var deleted = await client.PostAsync($"{Route}/{level.Guid}/excluir", null);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{level.Guid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"{Route}/{level.Guid}/excluir", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"{Route}/{level.Guid}", Payload(level.Code, level.Name, 1))).StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>($"{Route}?search={level.Code}");
        Assert.Equal(0, list.GetProperty("total").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.NiveisProfissionais.IgnoreQueryFilters().SingleAsync(x => x.Guid == level.Guid);
        Assert.True(stored.Excluido);
    }

    [Fact]
    public async Task Excluir_NivelEmUso_Retorna422ComMensagemDeNegocio()
    {
        using var client = AdminClient();
        var level = await CreateAsync(client, "U" + Suffix(), "Em uso " + Suffix(), 1);
        await SeedEmployeeAsync(level.Guid);

        var response = await client.PostAsync($"{Route}/{level.Guid}/excluir", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Route}/{level.Guid}")).StatusCode);
    }

    private HttpClient AdminClient() =>
        _factory.CreateApiClient(_factory.GrantPermissions(
            PermissionCodes.ViewProfessionalLevels,
            PermissionCodes.CreateProfessionalLevels,
            PermissionCodes.EditProfessionalLevels));

    private static object Payload(string code, string name, int order) => new { code, name, order };

    private static string Suffix() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task<LevelDto> CreateAsync(HttpClient client, string code, string name, int order)
    {
        var response = await client.PostAsJsonAsync(Route, Payload(code, name, order));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new LevelDto(
            body.GetProperty("guid").GetGuid(),
            body.GetProperty("code").GetString()!,
            body.GetProperty("name").GetString()!);
    }

    private static async Task AssertDuplicateAsync(HttpResponseMessage response, string field, string submittedValue)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("DUPLICATE_BUSINESS_KEY", problem.GetProperty("code").GetString());
        Assert.Equal(field, problem.GetProperty("field").GetString());
        Assert.DoesNotContain(submittedValue, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IX_", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlException", raw, StringComparison.Ordinal);
    }

    private async Task SeedEmployeeAsync(Guid levelGuid)
    {
        var now = DateTimeOffset.UtcNow;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var organization = new Organizacao(Guid.NewGuid(), "Rede " + Suffix(), now);
        db.Organizacoes.Add(organization);
        await db.SaveChangesAsync();
        var unit = new UnidadeHospitalar(Guid.NewGuid(), organization.Id, "Hospital " + Suffix(), now);
        var profession = new Profissao(Guid.NewGuid(), "Profissão " + Suffix(), null, now);
        var position = new Cargo(Guid.NewGuid(), "Cargo " + Suffix(), null, now);
        db.AddRange(unit, profession, position);
        await db.SaveChangesAsync();
        var levelId = await db.NiveisProfissionais.Where(x => x.Guid == levelGuid).Select(x => x.Id).SingleAsync();
        db.Funcionarios.Add(new Funcionario(
            Guid.NewGuid(), "Funcionário", $"f-{Suffix()}@hospital.test", null,
            profession.Id, position.Id, levelId, unit.Id, new DateOnly(2025, 1, 1), now));
        await db.SaveChangesAsync();
    }

    private sealed record LevelDto(Guid Guid, string Code, string Name);
}
