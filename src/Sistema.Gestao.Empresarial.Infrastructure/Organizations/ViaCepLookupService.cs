using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using static Sistema.Gestao.Empresarial.Infrastructure.Organizations.HospitalLookupHttp;

namespace Sistema.Gestao.Empresarial.Infrastructure.Organizations;

public sealed class ViaCepLookupService(HttpClient client, ILogger<ViaCepLookupService> logger) : ICepLookupService
{
    public Task<LookupResult<CepLookupResponse>> LookupAsync(string cep, CancellationToken cancellationToken)
    {
        if (!CadastroBrasileiro.CepValido(cep)) return Task.FromResult(new LookupResult<CepLookupResponse>(LookupOutcome.InvalidInput));
        var normalized = CadastroBrasileiro.SemMascaraCep(cep);
        return GetAsync(client, $"ws/{normalized}/json/", logger, json => Map(json, normalized), cancellationToken);
    }

    private static LookupResult<CepLookupResponse> Map(JsonElement json, string cep)
    {
        if (json.TryGetProperty("erro", out var error) && (error.ValueKind == JsonValueKind.True
            || error.ValueKind == JsonValueKind.String && error.GetString() == "true")) return new(LookupOutcome.NotFound);
        var returnedCep = Text(json, "cep");
        var city = Text(json, "localidade");
        var state = Text(json, "uf");
        if (CadastroBrasileiro.SemMascaraCep(returnedCep) != cep || string.IsNullOrWhiteSpace(city)
            || !CadastroBrasileiro.UfValida(state)) throw new JsonException();
        return new(LookupOutcome.Found, new(cep, Text(json, "logradouro"), Text(json, "complemento"),
            Text(json, "bairro"), city, state!, Text(json, "ibge"), Text(json, "ddd"), Text(json, "regiao")));
    }
}
