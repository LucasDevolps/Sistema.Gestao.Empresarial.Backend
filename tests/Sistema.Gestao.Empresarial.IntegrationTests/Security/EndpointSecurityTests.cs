using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;
using Sistema.Gestao.Empresarial.Api.Controllers;
using Sistema.Gestao.Empresarial.Api.Security;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Security;

public sealed class EndpointSecurityTests : IClassFixture<SecureApiFactory>
{
    private readonly SecureApiFactory _factory;

    public EndpointSecurityTests(SecureApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TodoEndpoint_DeveDeclararAutorizacaoOuExposicaoPublicaExplicitamente()
    {
        _factory.CreateClient();
        var endpoints = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is not null)
            .ToArray();

        var unsecured = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(endpoint => !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.Empty(unsecured);
    }

    [Fact]
    public void EndpointDeControllerNaoPublico_DeveExigirPermissaoCentralizada()
    {
        _factory.CreateClient();
        var controllerEndpoints = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!.ControllerTypeInfo.AsType() != typeof(AuthController));

        var withoutPermission = controllerEndpoints
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .All(data => data.Policy?.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal) != true))
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Empty(withoutPermission);
    }

    [Fact]
    public void Logout_DeveExigirPoliticaDeSessaoAtiva()
    {
        _factory.CreateClient();
        var logout = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "api/auth/logout");

        Assert.Contains(
            logout.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => authorization.Policy == AuthPolicies.ActiveSession);
    }

    [Fact]
    public void LoginERefresh_DevemExigirRateLimitDeAutenticacao()
    {
        _factory.CreateClient();
        var authenticationEndpoints = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is "api/auth/login" or "api/auth/refresh")
            .ToArray();

        Assert.Equal(2, authenticationEndpoints.Length);
        Assert.All(authenticationEndpoints, endpoint =>
            Assert.Equal(
                RateLimitPolicies.Authentication,
                endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName));
    }

    [Fact]
    public void Api_NaoDeveExporEndpointsHttpDelete()
    {
        _factory.CreateClient();
        var deleteEndpoints = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                .Contains(HttpMethods.Delete, StringComparer.OrdinalIgnoreCase) == true)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Empty(deleteEndpoints);
    }
}

public sealed class SecureApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SqlServer", "Server=localhost;Database=sge_tests;Integrated Security=true;TrustServerCertificate=true");
        builder.UseSetting("Jwt:Issuer", "tests");
        builder.UseSetting("Jwt:Audience", "tests");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-with-at-least-thirty-two-characters");
        builder.UseSetting("Redis:Configuration", "localhost:6379,abortConnect=false");
        builder.UseSetting("Redis:InstanceName", "sge-tests");
        builder.UseSetting("RabbitMq:Host", "localhost");
        builder.UseSetting("RabbitMq:Username", "tests");
        builder.UseSetting("RabbitMq:Password", "tests");
        builder.UseSetting("OpenTelemetry:Enabled", "false");
    }
}
