using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Infrastructure.Organizations;
using Sistema.Gestao.Empresarial.IntegrationTests.Api;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Organizations;

public sealed class HospitalLookupServiceTests
{
    [Fact]
    public async Task InactiveRegistration_IsAnAlert_AndManualCreateRemainsAllowed()
    {
        using var client = new HttpClient(new HospitalLookupApiTests.FakeHandler(HttpStatusCode.OK,
            """{"cnpj":"11222333000181","razao_social":"Empresa","situacao_cadastral":8,"descricao_situacao_cadastral":"BAIXADA"}"""))
        { BaseAddress = new Uri("https://example.test") };
        var lookup = new BrasilApiCnpjLookupService(client, NullLogger<BrasilApiCnpjLookupService>.Instance);
        var result = await lookup.LookupAsync("11.222.333/0001-81", default);
        Assert.Equal(LookupOutcome.Found, result.Outcome);
        Assert.True(result.Data!.InactiveRegistrationWarning);
        Assert.Equal("BAIXADA", result.Data.RegistrationStatus);
        await using var f = await OrganizationCatalogFixture.CreateAsync();
        var created = await f.Service.CreateHospitalUnitAsync(HospitalUnitServiceTests.Request() with
        {
            Cnpj = result.Data.Cnpj, RegistrationStatus = result.Data.RegistrationStatus
        }, new(f.Actor.Guid, Guid.NewGuid(), "inactive-cnpj", null), default);
        Assert.Equal("BAIXADA", created.RegistrationStatus);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RequestCancellation_IsPropagatedInsteadOfBeingReportedAsProviderFailure(bool cnpj)
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new CancelHandler(cancellation)) { BaseAddress = new Uri("https://example.test") };
        if (cnpj)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BrasilApiCnpjLookupService(client, NullLogger<BrasilApiCnpjLookupService>.Instance)
                .LookupAsync("11222333000181", cancellation.Token));
        else
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ViaCepLookupService(client, NullLogger<ViaCepLookupService>.Instance)
                .LookupAsync("01001000", cancellation.Token));
    }

    private sealed class CancelHandler(CancellationTokenSource cancellation) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The caller's token must reach the HTTP handler.");
        }
    }
}
