using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace Sistema.Gestao.Empresarial.Api.Auditing;

public interface ISensitiveDataRedactor
{
    string RedactHeaders(IHeaderDictionary headers);
    string? RedactQuery(IQueryCollection query);
    string? RedactBody(string? body, string? contentType, bool truncated);
}

public sealed partial class SensitiveDataRedactor : ISensitiveDataRedactor
{
    public const string Redacted = "***REDACTED***";
    public const string Truncated = "***REDACTED_TRUNCATED_BODY***";
    public const string Unsupported = "***BODY_NOT_CAPTURED_FOR_CONTENT_TYPE***";
    public const string InvalidJson = "***REDACTED_INVALID_JSON***";

    public string RedactHeaders(IHeaderDictionary headers) => SerializeCollection(headers);

    public string? RedactQuery(IQueryCollection query) =>
        query.Count == 0 ? null : SerializeCollection(query);

    public string? RedactBody(string? body, string? contentType, bool truncated)
    {
        if (string.IsNullOrEmpty(body))
            return null;
        if (truncated)
            return Truncated;

        if (IsJson(contentType))
            return RedactJson(body);
        if (contentType?.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true)
            return SerializeCollection(QueryHelpers.ParseQuery(body));

        return Unsupported;
    }

    private static string RedactJson(string body)
    {
        try
        {
            var node = JsonNode.Parse(
                body,
                new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                new JsonDocumentOptions { MaxDepth = 64 });
            RedactNode(node);
            return node?.ToJsonString() ?? "null";
        }
        catch (JsonException)
        {
            return InvalidJson;
        }
    }

    private static void RedactNode(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToArray())
            {
                if (IsSensitiveName(property.Key))
                {
                    jsonObject[property.Key] = Redacted;
                }
                else
                {
                    RedactNode(property.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                RedactNode(item);
        }
        else if (node is JsonValue value
                 && value.TryGetValue<string>(out var text)
                 && BearerTokenRegex().IsMatch(text))
        {
            value.ReplaceWith(Redacted);
        }
    }

    private static string SerializeCollection(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> values)
    {
        var result = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            result[pair.Key] = IsSensitiveName(pair.Key)
                ? Redacted
                : pair.Value.Count switch
                {
                    0 => string.Empty,
                    1 => RedactValue(pair.Value[0]),
                    _ => pair.Value.Select(RedactValue).ToArray()
                };
        }
        return JsonSerializer.Serialize(result);
    }

    private static string? RedactValue(string? value) =>
        value is not null && BearerTokenRegex().IsMatch(value) ? Redacted : value;

    private static bool IsSensitiveName(string name)
    {
        var normalized = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return normalized.Contains("password", StringComparison.Ordinal)
               || normalized.Contains("senha", StringComparison.Ordinal)
               || normalized.Contains("authorization", StringComparison.Ordinal)
               || normalized.Contains("token", StringComparison.Ordinal)
               || normalized.Contains("cookie", StringComparison.Ordinal)
               || normalized.Contains("apikey", StringComparison.Ordinal)
               || normalized.Contains("secret", StringComparison.Ordinal)
               || normalized.Contains("credential", StringComparison.Ordinal)
               || normalized.Contains("signingkey", StringComparison.Ordinal)
               || normalized.Contains("encryptionkey", StringComparison.Ordinal);
    }

    private static bool IsJson(string? contentType) =>
        contentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true
        || contentType?.Contains("+json", StringComparison.OrdinalIgnoreCase) == true;

    [GeneratedRegex(@"\bBearer\s+[^\s,;]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex BearerTokenRegex();
}
