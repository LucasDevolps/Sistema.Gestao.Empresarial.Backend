namespace Sistema.Gestao.Empresarial.Api.Security;

public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";
    public string[] AllowedOrigins { get; init; } = [];
}

public static class FrontendCorsConfiguration
{
    public const string PolicyName = "Frontend";

    public static IServiceCollection AddFrontendCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(FrontendCorsOptions.SectionName);
        services.AddOptions<FrontendCorsOptions>()
            .Bind(section)
            .Validate(options => options.AllowedOrigins.All(origin => IsValidOrigin(origin, environment.IsDevelopment())),
                "Cors:AllowedOrigins deve conter origens HTTPS exatas, sem wildcard, caminho, credenciais, query ou fragmento. HTTP loopback somente em Development.")
            .ValidateOnStart();

        var origins = section.Get<FrontendCorsOptions>()?.AllowedOrigins ?? [];
        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "HEAD", "POST", "PUT", "PATCH", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "X-Correlation-ID")
            .WithExposedHeaders("Retry-After", "X-Correlation-ID")));
        return services;
    }

    public static bool IsValidOrigin(string origin, bool development) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && !origin.Contains('*')
        && uri.UserInfo.Length == 0
        && uri.AbsolutePath == "/"
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && origin == uri.GetLeftPart(UriPartial.Authority)
        && (uri.Scheme == Uri.UriSchemeHttps
            || (development && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
}
