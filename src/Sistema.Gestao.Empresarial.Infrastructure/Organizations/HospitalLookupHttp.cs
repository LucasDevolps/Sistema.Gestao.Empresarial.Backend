using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sistema.Gestao.Empresarial.Application.Organizations;

namespace Sistema.Gestao.Empresarial.Infrastructure.Organizations;

internal static class HospitalLookupHttp
{
    internal static async Task<LookupResult<T>> GetAsync<T>(HttpClient client, string path, ILogger logger,
        Func<JsonElement, LookupResult<T>> map, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(LookupOutcome.NotFound);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Consulta cadastral indisponível. Status={Status}", (int)response.StatusCode);
                return new(LookupOutcome.Unavailable);
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            return map(document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Timeout na consulta cadastral auxiliar.");
            return new(LookupOutcome.Unavailable);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        {
            logger.LogWarning("Falha na consulta cadastral auxiliar. Tipo={Type}", exception.GetType().Name);
            return new(LookupOutcome.Unavailable);
        }
    }

    internal static string? Text(JsonElement json, string name)
    {
        if (!json.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => throw new JsonException()
        };
    }
}
