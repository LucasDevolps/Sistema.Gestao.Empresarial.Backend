using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using static Sistema.Gestao.Empresarial.Infrastructure.Organizations.HospitalLookupHttp;

namespace Sistema.Gestao.Empresarial.Infrastructure.Organizations;

public sealed class BrasilApiCnpjLookupService(HttpClient client, ILogger<BrasilApiCnpjLookupService> logger) : ICnpjLookupService
{
    public Task<LookupResult<CnpjLookupResponse>> LookupAsync(string cnpj, CancellationToken cancellationToken)
    {
        if (!CadastroBrasileiro.CnpjValido(cnpj)) return Task.FromResult(new LookupResult<CnpjLookupResponse>(LookupOutcome.InvalidInput));
        var normalized = CadastroBrasileiro.SemMascaraCnpj(cnpj);
        return GetAsync(client, $"api/cnpj/v1/{normalized}", logger, json => Map(json, normalized), cancellationToken);
    }

    private static LookupResult<CnpjLookupResponse> Map(JsonElement json, string cnpj)
    {
        var legalName = Text(json, "razao_social");
        if (string.IsNullOrWhiteSpace(legalName) || Text(json, "cnpj") != cnpj) throw new JsonException();
        DateOnly? openingDate = null;
        var opening = Text(json, "data_inicio_atividade");
        if (!string.IsNullOrWhiteSpace(opening))
        {
            if (!DateOnly.TryParseExact(opening, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw new JsonException();
            openingDate = date;
        }
        List<CnaeLookupResponse> secondary = [];
        if (json.TryGetProperty("cnaes_secundarios", out var activities) && activities.ValueKind != JsonValueKind.Null)
        {
            if (activities.ValueKind != JsonValueKind.Array) throw new JsonException();
            foreach (var item in activities.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) throw new JsonException();
                secondary.Add(new(Text(item, "codigo"), Text(item, "descricao")));
            }
        }
        var statusCode = Text(json, "situacao_cadastral");
        var status = Text(json, "descricao_situacao_cadastral");
        var warning = statusCode is not null ? statusCode != "2"
            : !string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ATIVA", StringComparison.OrdinalIgnoreCase);
        return new(LookupOutcome.Found, new(cnpj, legalName, Text(json, "nome_fantasia"), status, openingDate,
            Text(json, "natureza_juridica"), new(Text(json, "cnae_fiscal"), Text(json, "cnae_fiscal_descricao")), secondary,
            Text(json, "ddd_telefone_1"), Text(json, "email"), NormalizeOptionalCep(Text(json, "cep")),
            Text(json, "logradouro"), Text(json, "numero"), Text(json, "complemento"), Text(json, "bairro"),
            Text(json, "municipio"), Text(json, "uf"), warning));
    }

    private static string? NormalizeOptionalCep(string? cep) => string.IsNullOrWhiteSpace(cep) ? null
        : CadastroBrasileiro.CepValido(cep) ? CadastroBrasileiro.SemMascaraCep(cep) : throw new JsonException();
}
