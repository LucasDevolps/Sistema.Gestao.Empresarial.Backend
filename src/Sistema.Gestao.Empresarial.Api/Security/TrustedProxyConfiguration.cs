using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Sistema.Gestao.Empresarial.Api.Security;

public static class TrustedProxyConfiguration
{
    public static IServiceCollection AddTrustedProxy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetRequiredSection(ReverseProxyOptions.SectionName);
        services.AddOptions<ReverseProxyOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(
                options => !options.Enabled || options.KnownProxies.Length > 0,
                "Ao habilitar o proxy reverso, informe ao menos um endereço em ReverseProxy:KnownProxies.")
            .Validate(
                options => options.KnownProxies.All(value => IPAddress.TryParse(value, out _)),
                "ReverseProxy:KnownProxies contém um endereço IP inválido.")
            .Validate(
                options => !options.UseCloudflareHeaders || (options.Enabled
                    && options.ForwardLimit == 1
                    && options.KnownProxies.Length > 0
                    && options.KnownProxies.All(value => IPAddress.TryParse(value, out var address)
                        && IPAddress.IsLoopback(address))),
                "O perfil Cloudflare exige proxy habilitado, ForwardLimit=1 e proxies exclusivamente de loopback.")
            .ValidateOnStart();

        var proxy = section.Get<ReverseProxyOptions>()
            ?? throw new OptionsValidationException(
                ReverseProxyOptions.SectionName,
                typeof(ReverseProxyOptions),
                ["A seção ReverseProxy é obrigatória."]);
        if (!proxy.Enabled)
        {
            return services;
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            if (proxy.UseCloudflareHeaders)
            {
                // IIS in-process receives cloudflared directly over loopback. Cloudflare
                // overwrites this single-IP header; do not trust the visitor's XFF chain.
                options.ForwardedForHeaderName = "CF-Connecting-IP";
                options.RequireHeaderSymmetry = true;
            }
            else
            {
                options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost;
                foreach (var host in (configuration["AllowedHosts"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    options.AllowedHosts.Add(host.Trim());
                }
            }
            options.ForwardLimit = proxy.ForwardLimit;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var knownProxy in proxy.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(knownProxy));
            }
        });
        if (proxy.UseCloudflareHeaders)
        {
            services.AddHttpsRedirection(options => options.HttpsPort = 443);
        }
        return services;
    }

    public static bool IsLocalIisRequest(HttpContext context) =>
        string.Equals(context.Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        && context.Connection.RemoteIpAddress is { } address
        && IPAddress.IsLoopback(address);
}
