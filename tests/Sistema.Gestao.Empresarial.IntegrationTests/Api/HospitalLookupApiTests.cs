using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

public sealed class HospitalLookupApiTests
{
    [Theory]
    [InlineData("cnpj", "11222333000181", "{\"cnpj\":\"11222333000181\",\"razao_social\":\"Hospital Ltda\",\"nome_fantasia\":\"Hospital\",\"situacao_cadastral\":2,\"descricao_situacao_cadastral\":\"ATIVA\",\"data_inicio_atividade\":\"2000-01-01\",\"natureza_juridica\":\"Sociedade\",\"cnae_fiscal\":8610101,\"cnaes_secundarios\":[{\"codigo\":8610102,\"descricao\":\"Atendimento\"}],\"ddd_telefone_1\":\"1133334444\",\"email\":\"hospital@test.test\",\"cep\":\"01001000\",\"logradouro\":\"Praça\",\"numero\":\"10\",\"complemento\":\"A\",\"bairro\":\"Sé\",\"municipio\":\"São Paulo\",\"uf\":\"SP\"}")]
    [InlineData("cep", "01001-000", "{\"cep\":\"01001-000\",\"logradouro\":\"Praça da Sé\",\"complemento\":\"lado ímpar\",\"bairro\":\"Sé\",\"localidade\":\"São Paulo\",\"uf\":\"SP\",\"ibge\":\"3550308\",\"ddd\":\"11\",\"regiao\":\"Sudeste\"}")]
    public async Task Success_MapsProviderFields(string type, string value, string payload)
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        var actor = await HospitalUnitsApiTests.SeedActor(factory);
        var handler = new FakeHandler(HttpStatusCode.OK, payload);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add(ProfessionalLevelsApiFactory.UserHeader, actor.ToString());
        var response = await client.GetAsync($"/api/unidades-hospitalares/consulta-{type}?{type}={value}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("SP", body!["state"]!.GetValue<string>());
        Assert.Equal("01001000", body["postalCode"]!.GetValue<string>());
        Assert.Equal(1, handler.Calls);
        Assert.Contains(type == "cnpj" ? "11222333000181" : "01001000", handler.LastUri!.AbsolutePath);
        if (type == "cep") Assert.Null(body["number"]);
        else
        {
            Assert.Equal("Hospital Ltda", body["legalName"]!.GetValue<string>());
            Assert.False(body["inactiveRegistrationWarning"]!.GetValue<bool>());
            Assert.Single(body["secondaryCnaes"]!.AsArray());
        }
    }

    [Theory]
    [InlineData("cnpj", "11222333000182")]
    [InlineData("cep", "01001A00")]
    public async Task InvalidIdentifier_DoesNotCallProvider(string type, string value)
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        var actor = await HospitalUnitsApiTests.SeedActor(factory);
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add(ProfessionalLevelsApiFactory.UserHeader, actor.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/unidades-hospitalares/consulta-{type}?{type}={value}")).StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("cnpj", 404, "", 404)]
    [InlineData("cep", 200, "{\"erro\":true}", 404)]
    [InlineData("cep", 200, "{\"erro\":\"true\"}", 404)]
    [InlineData("cnpj", 500, "", 503)]
    [InlineData("cep", 500, "", 503)]
    [InlineData("cnpj", 429, "", 503)]
    [InlineData("cep", 429, "", 503)]
    [InlineData("cnpj", 200, "invalid json", 503)]
    [InlineData("cep", 200, "{}", 503)]
    [InlineData("cnpj", 200, "{}", 503)]
    [InlineData("cnpj", 0, "timeout", 503)]
    [InlineData("cep", 0, "timeout", 503)]
    [InlineData("cnpj", 0, "dns", 503)]
    [InlineData("cep", 0, "dns", 503)]
    public async Task ProviderFailure_IsControlledAndManualCreateStillWorks(string type, int providerStatus, string payload, int expectedStatus)
    {
        await using var factory = new ProfessionalLevelsApiFactory();
        var actor = await HospitalUnitsApiTests.SeedActor(factory);
        var handler = new FakeHandler((HttpStatusCode)providerStatus, payload);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add(ProfessionalLevelsApiFactory.UserHeader, actor.ToString());
        var value = type == "cnpj" ? "11222333000181" : "01001000";
        var lookup = await client.GetAsync($"/api/unidades-hospitalares/consulta-{type}?{type}={value}");
        Assert.Equal(expectedStatus, (int)lookup.StatusCode);
        var created = await client.PostAsJsonAsync("/api/unidades-hospitalares", HospitalUnitsApiTests.Payload());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    internal sealed class FakeHandler(HttpStatusCode status, string payload) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Uri? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; LastUri = request.RequestUri;
            if (payload == "timeout") throw new TaskCanceledException();
            if (payload == "dns") throw new HttpRequestException("provider detail must not leak");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") });
        }
    }
}
