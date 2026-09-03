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
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = proxy.ForwardLimit;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var knownProxy in proxy.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(knownProxy));
            }
        });
        return services;
    }
}
