using System.ComponentModel.DataAnnotations;

namespace Sistema.Gestao.Empresarial.Api.Security;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public bool Enabled { get; init; }

    [Range(1, 5)]
    public int ForwardLimit { get; init; } = 1;

    public string[] KnownProxies { get; init; } = [];
}

public sealed class ApiRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 100_000)]
    public int GlobalPermitLimit { get; init; } = 300;

    [Range(1, 3_600)]
    public int GlobalWindowSeconds { get; init; } = 60;

    [Range(1, 10_000)]
    public int AuthenticationPermitLimit { get; init; } = 5;

    [Range(1, 3_600)]
    public int AuthenticationWindowSeconds { get; init; } = 60;
}

public sealed class KestrelSecurityOptions
{
    public const string SectionName = "KestrelSecurity";

    [Range(1_024, 100 * 1_024 * 1_024)]
    public long MaxRequestBodySizeBytes { get; init; } = 1_048_576;

    [Range(1, 300)]
    public int RequestHeadersTimeoutSeconds { get; init; } = 10;

    [Range(1, 300)]
    public int KeepAliveTimeoutSeconds { get; init; } = 30;

    [Range(1, 1_000_000)]
    public double MinRequestBodyDataRateBytesPerSecond { get; init; } = 240;

    [Range(1, 60)]
    public int MinRequestBodyDataRateGracePeriodSeconds { get; init; } = 5;
}

public static class RateLimitPolicies
{
    public const string Authentication = "authentication";
}
