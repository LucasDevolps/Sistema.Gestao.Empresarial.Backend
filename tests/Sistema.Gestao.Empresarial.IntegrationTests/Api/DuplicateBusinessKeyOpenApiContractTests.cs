using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Sistema.Gestao.Empresarial.IntegrationTests.Security;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Api;

/// <summary>
/// Garante que o contrato exposto no OpenAPI/Swagger (derivado dos
/// <c>[ProducesResponseType]</c>) acompanha o comportamento real: os cadastros que
/// podem rejeitar por duplicidade declaram <c>409</c>, e nada declara status que não
/// pode ocorrer.
/// </summary>
public sealed class DuplicateBusinessKeyOpenApiContractTests : IClassFixture<SecureApiFactory>
{
    private readonly SecureApiFactory _factory;

    public DuplicateBusinessKeyOpenApiContractTests(SecureApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("POST", "api/profissoes", new[] { 201, 400, 409, 422 })]
    [InlineData("PUT", "api/profissoes/{professionGuid}", new[] { 200, 400, 404, 409, 422 })]
    [InlineData("POST", "api/cargos", new[] { 201, 400, 409, 422 })]
    [InlineData("PUT", "api/cargos/{positionGuid}", new[] { 200, 400, 404, 409, 422 })]
    [InlineData("POST", "api/funcionarios", new[] { 201, 400, 409, 422 })]
    [InlineData("PUT", "api/funcionarios/{employeeGuid}", new[] { 200, 400, 404, 409, 422 })]
    public void CadastrosComDuplicidade_DevemDeclararExatamenteOsStatusReais(
        string httpMethod,
        string relativePath,
        int[] expectedStatusCodes)
    {
        _factory.CreateClient();
        var description = _factory.Services
            .GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Single(item =>
                string.Equals(item.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase)
                && item.RelativePath == relativePath);

        var declared = description.SupportedResponseTypes
            .Select(response => response.StatusCode)
            .Where(status => status >= 200)
            .Distinct()
            .OrderBy(status => status)
            .ToArray();

        Assert.Equal(expectedStatusCodes.OrderBy(status => status).ToArray(), declared);
    }
}
