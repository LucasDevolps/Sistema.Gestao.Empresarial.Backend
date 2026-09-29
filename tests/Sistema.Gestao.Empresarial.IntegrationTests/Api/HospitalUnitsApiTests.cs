using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

public sealed class HospitalUnitsApiTests
{
    private const string Route = "/api/unidades-hospitalares";

    internal static JsonObject Payload() => new()
    {
        ["name"] = "Hospital Central",
        ["legalName"] = "Hospital Central Ltda",
        ["cnpj"] = "11.222.333/0001-81",
        ["cnes"] = "1234567",
        ["internalCode"] = " HC ",
        ["postalCode"] = "01001-000",
        ["street"] = "Praça da Sé",
        ["number"] = "10",
        ["district"] = "Sé",
        ["city"] = "São Paulo",
        ["state"] = "sp",
        ["totalBeds"] = 20,
        ["icuBeds"] = 4,
        ["administrativeResponsibleName"] = "Maria",
        ["technicalResponsibleCouncil"] = "CRM",
        ["clinicalDirectorCrm"] = "12345",
        ["sanitaryPermit"] = "ALV-1",
        ["hasEmergencyRoom"] = true
    };

    [Fact]
    public async Task Crud_PreservesCompleteDataAndAuditsEachMutation()
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        var actor = await SeedActor(factory);
        using var client = factory.CreateApiClient(actor);
        var response = await client.PostAsJsonAsync(Route, Payload());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<JsonObject>();
        var guid = created!["guid"]!.GetValue<Guid>();
        Assert.Equal("11222333000181", created["cnpj"]!.GetValue<string>());
        Assert.Equal("01001000", created["postalCode"]!.GetValue<string>());
        Assert.Equal("SP", created["state"]!.GetValue<string>());
        Assert.Null(created["id"]);
        var detail = await client.GetFromJsonAsync<JsonObject>($"{Route}/{guid}");
        Assert.Equal("Maria", detail!["administrativeResponsibleName"]!.GetValue<string>());
        Assert.Equal("ALV-1", detail["sanitaryPermit"]!.GetValue<string>());
        var update = Payload();
        update["name"] = "Hospital Atualizado";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{Route}/{guid}", update)).StatusCode);
        foreach (var active in new[] { false, true })
        {
            var status = await client.PatchAsJsonAsync($"{Route}/{guid}/status", new { active });
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            Assert.Equal(active, (await status.Content.ReadFromJsonAsync<JsonObject>())!["active"]!.GetValue<bool>());
        }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audits = await db.AuditLogs.Where(x => x.EntidadeGuid == guid).ToListAsync();
        Assert.Equal(4, audits.Count);
        Assert.All(audits, x => Assert.Equal(actor, x.UsuarioGuid));
        Assert.Equal(4, await db.OutboxMessages.CountAsync());
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("cnpj", "11.222.333/0001-82")]
    [InlineData("postalCode", "01001A00")]
    [InlineData("legalName", "")]
    [InlineData("state", "XX")]
    [InlineData("email", "invalid")]
    [InlineData("website", "javascript:alert(1)")]
    public async Task InvalidInput_Returns400(string field, string value)
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        using var client = factory.CreateApiClient(await SeedActor(factory));
        var request = Payload(); request[field] = value;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, request)).StatusCode);
    }

    [Theory]
    [InlineData("cnpj")]
    [InlineData("cnes")]
    [InlineData("internalCode")]
    public async Task DuplicateKeys_Return409WithSafeField(string field)
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        using var client = factory.CreateApiClient(await SeedActor(factory));
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Route, Payload())).StatusCode);
        var second = Payload(); second["name"] = "Outro hospital";
        second["cnpj"] = null; second["cnes"] = null; second["internalCode"] = null;
        second[field] = Payload()[field]!.DeepClone();
        var conflict = await client.PostAsJsonAsync(Route, second);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(field, (await conflict.Content.ReadFromJsonAsync<JsonObject>())!["field"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task UnauthorizedRequests_AreRejected(bool authenticated, HttpStatusCode expected)
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        using var client = factory.CreateApiClient(authenticated ? factory.GrantPermissions() : null);
        var guid = Guid.NewGuid();
        Assert.Equal(expected, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(expected, (await client.GetAsync($"{Route}/{guid}")).StatusCode);
        Assert.Equal(expected, (await client.PostAsJsonAsync(Route, Payload())).StatusCode);
        Assert.Equal(expected, (await client.PutAsJsonAsync($"{Route}/{guid}", Payload())).StatusCode);
        Assert.Equal(expected, (await client.PatchAsJsonAsync($"{Route}/{guid}/status", new { active = false })).StatusCode);
        Assert.Equal(expected, (await client.GetAsync($"{Route}/consulta-cnpj?cnpj=11222333000181")).StatusCode);
        Assert.Equal(expected, (await client.GetAsync($"{Route}/consulta-cep?cep=01001000")).StatusCode);
    }

    [Fact]
    public async Task MissingAndForeignUnits_Return404_OrganizationCannotBeReassigned()
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        var actor = await SeedActor(factory);
        var otherActor = await SeedActor(factory);
        using var client = factory.CreateApiClient(actor);
        using var other = factory.CreateApiClient(otherActor);
        var created = await (await client.PostAsJsonAsync(Route, Payload())).Content.ReadFromJsonAsync<JsonObject>();
        var guid = created!["guid"]!.GetValue<Guid>();
        foreach (var missing in new[] { guid, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Route}/{missing}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"{Route}/{missing}", Payload())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync($"{Route}/{missing}/status", new { active = false })).StatusCode);
        }
        var request = Payload();
        request["organizationGuid"] = created["organization"]!["guid"]!.DeepClone();
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync(Route, request)).StatusCode);
        var own = await other.GetFromJsonAsync<JsonObject>(Route);
        Assert.DoesNotContain(own!["items"]!.AsArray(), x => x!["guid"]!.GetValue<Guid>() == guid);
    }

    [Fact]
    public async Task ReadPermission_DoesNotAuthorizeWrites_EmployeePermissionDoesNotAuthorizeHospitals()
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        using var reader = factory.CreateApiClient(await SeedActor(factory, "UNIDADE_HOSPITALAR_VISUALIZAR"));
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(Route, Payload())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", Payload())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PatchAsJsonAsync($"{Route}/{Guid.NewGuid()}/status", new { active = false })).StatusCode);
        using var employeeReader = factory.CreateApiClient(await SeedActor(factory, "FUNCIONARIO_VISUALIZAR"));
        Assert.Equal(HttpStatusCode.Forbidden, (await employeeReader.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task AllRegistrationSections_RoundTripThroughCreateAndUpdate_ListStaysSmall()
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        using var client = factory.CreateApiClient(await SeedActor(factory));
        var payload = Payload();
        payload["hasOwnCnpj"] = true;
        payload["unitType"] = "HospitalGeral";
        payload["nature"] = "Privada";
        payload["acronym"] = "Texto";
        payload["activityStartDate"] = "2026-01-02";
        payload["registrationStatus"] = "Texto";
        payload["openingDate"] = "2026-01-02";
        payload["legalNature"] = "Texto";
        payload["primaryCnae"] = "Texto";
        payload["secondaryCnaes"] = "Texto";
        payload["stateRegistration"] = "Texto";
        payload["municipalRegistration"] = "Texto";
        payload["complement"] = "Texto";
        payload["ibgeCode"] = "3550308";
        payload["region"] = "Texto";
        payload["areaCode"] = "11";
        payload["addressReference"] = "Texto";
        payload["phone"] = "+55 11 3333-4444";
        payload["secondaryPhone"] = "+55 11 3333-4444";
        payload["whatsapp"] = "+55 11 3333-4444";
        payload["email"] = "contato@hospital.test";
        payload["administrativeEmail"] = "contato@hospital.test";
        payload["website"] = "https://hospital.test";
        payload["extension"] = "Texto";
        payload["administrativeResponsibleRole"] = "Texto";
        payload["administrativeResponsibleEmail"] = "contato@hospital.test";
        payload["administrativeResponsiblePhone"] = "+55 11 3333-4444";
        payload["technicalResponsibleName"] = "Texto";
        payload["technicalResponsibleProfession"] = "Texto";
        payload["technicalResponsibleCouncilNumber"] = "Texto";
        payload["technicalResponsibleCouncilState"] = "SP";
        payload["technicalResponsibleEmail"] = "contato@hospital.test";
        payload["technicalResponsiblePhone"] = "+55 11 3333-4444";
        payload["clinicalDirectorName"] = "Texto";
        payload["clinicalDirectorCrmState"] = "SP";
        payload["clinicalDirectorEmail"] = "contato@hospital.test";
        payload["clinicalDirectorPhone"] = "+55 11 3333-4444";
        payload["sanitaryPermitExpiry"] = "2026-01-02";
        payload["operatingLicense"] = "Texto";
        payload["operatingLicenseExpiry"] = "2026-01-02";
        payload["regulatoryNotes"] = "Texto";
        payload["open24Hours"] = true;
        payload["hasInpatientCare"] = true;
        payload["hasIcu"] = true;
        payload["hasSurgicalCenter"] = true;
        payload["hasMaternity"] = true;
        payload["hasOutpatientCare"] = true;
        payload["notes"] = "Texto";
        var response = await client.PostAsJsonAsync(Route, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonObject>();
        var guid = created!["guid"]!.GetValue<Guid>();
        payload["cnpj"] = "11222333000181";
        payload["postalCode"] = "01001000";
        payload["internalCode"] = "HC";
        payload["state"] = "SP";
        payload["acronym"] = "TEXTO";
        foreach (var item in payload) Assert.True(JsonNode.DeepEquals(item.Value, created[item.Key]), item.Key);
        payload["notes"] = "Observação atualizada";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{Route}/{guid}", payload)).StatusCode);
        var detail = await client.GetFromJsonAsync<JsonObject>($"{Route}/{guid}");
        foreach (var item in payload) Assert.True(JsonNode.DeepEquals(item.Value, detail![item.Key]), item.Key);
        var page = await client.GetFromJsonAsync<JsonObject>($"{Route}?cnpj=11222333000181");
        var summary = Assert.Single(page!["items"]!.AsArray())!;
        Assert.Null(summary["administrativeResponsibleName"]);
        Assert.Null(summary["regulatoryNotes"]);
        Assert.Equal("São Paulo", summary["city"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 2)]
    public async Task InvalidBeds_Return400OnCreateAndUpdate(int total, int icu)
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        using var client = factory.CreateApiClient(await SeedActor(factory));
        var payload = Payload(); payload["totalBeds"] = total; payload["icuBeds"] = icu;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", payload)).StatusCode);
    }

    [Theory]
    [InlineData("cnpj")]
    [InlineData("cnes")]
    public async Task GlobalKeysCrossOrganizations_InternalCodeCanRepeat(string field)
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        using var first = factory.CreateApiClient(await SeedActor(factory));
        using var second = factory.CreateApiClient(await SeedActor(factory));
        Assert.Equal(HttpStatusCode.Created, (await first.PostAsJsonAsync(Route, Payload())).StatusCode);
        var payload = Payload();
        payload[field == "cnpj" ? "cnes" : "cnpj"] = null;
        var conflict = await second.PostAsJsonAsync(Route, payload);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(field, (await conflict.Content.ReadFromJsonAsync<JsonObject>())!["field"]!.GetValue<string>());
        payload[field] = null;
        Assert.Equal(HttpStatusCode.Created, (await second.PostAsJsonAsync(Route, payload)).StatusCode);
    }

    [Fact]
    public async Task OpenApi_ExposesNewRoutesAndRegistrationSchema()
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        await using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Swagger:Enabled", "true"));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var document = await client.GetFromJsonAsync<JsonObject>("/swagger/v1/swagger.json");
        var paths = document!["paths"]!;
        Assert.NotNull(paths[Route]!["post"]!["responses"]!["201"]);
        Assert.NotNull(paths[$"{Route}/{{unitGuid}}"]!["put"]!["responses"]!["409"]);
        Assert.NotNull(paths[$"{Route}/{{unitGuid}}/status"]!["patch"]);
        Assert.NotNull(paths[$"{Route}/consulta-cnpj"]!["get"]!["responses"]!["503"]);
        Assert.NotNull(paths[$"{Route}/consulta-cep"]!["get"]!["responses"]!["404"]);
        Assert.NotNull(document["components"]!["schemas"]!["HospitalUnitRegistrationRequest"]!["properties"]!["administrativeResponsibleName"]);
    }

    internal static async Task<Guid> SeedActor(ProfessionalLevelsApiFactory factory, params string[] permissions)
    {
        var actor = factory.GrantPermissions(permissions.Length == 0
            ? ["UNIDADE_HOSPITALAR_VISUALIZAR", "UNIDADE_HOSPITALAR_CRIAR", "UNIDADE_HOSPITALAR_EDITAR"]
            : permissions);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var organization = new Organizacao(Guid.NewGuid(), $"Rede {actor}", now);
        db.Add(organization); await db.SaveChangesAsync();
        var unit = new UnidadeHospitalar(Guid.NewGuid(), organization.Id, "Unidade legada", now);
        var profession = new Profissao(Guid.NewGuid(), "Profissão", null, now);
        var position = new Cargo(Guid.NewGuid(), "Cargo", null, now);
        var level = new NivelProfissional(Guid.NewGuid(), "NS", "Nivel", 1, now);
        db.AddRange(unit, profession, position, level); await db.SaveChangesAsync();
        var employee = new Funcionario(Guid.NewGuid(), "Administrador", $"{actor}@test.test", null,
            profession.Id, position.Id, level.Id, unit.Id, new DateOnly(2025, 1, 1), now);
        db.Add(employee); await db.SaveChangesAsync();
        db.Add(new Usuario(actor, employee.Id, employee.Email, "TEST", now));
        await db.SaveChangesAsync();
        return actor;
    }
}
