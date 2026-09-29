using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Organizations;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Organizations;

public sealed class HospitalUnitServiceTests
{
    internal static HospitalUnitRegistrationRequest Request(string name = "Hospital Novo") => new()
    {
        Name = name, LegalName = "Empresa Hospitalar", PostalCode = "01001-000", Street = "Praça da Sé",
        Number = "10", District = "Sé", City = "São Paulo", State = "SP"
    };
    private static HospitalUnitOperationContext Context(OrganizationCatalogFixture f) =>
        new(f.Actor.Guid, Guid.NewGuid(), "trace-hospital", "127.0.0.1");

    [Fact]
    public async Task StatusPreservesSectorsEmployeesAndHistoricalRelationships()
    {
        await using var f = await OrganizationCatalogFixture.CreateAsync();
        var link = new FuncionarioUnidadeAtuacao(Guid.NewGuid(), f.ActorEmployee.Id, f.PrimaryUnit.Id, new DateOnly(2025, 1, 1), f.Clock.GetUtcNow());
        f.Db.Add(link); await f.Db.SaveChangesAsync();
        var context = Context(f);
        await f.Service.ChangeHospitalUnitStatusAsync(f.PrimaryUnit.Guid, false, context, default);
        f.Db.ChangeTracker.Clear();
        Assert.False((await f.Db.UnidadesHospitalares.SingleAsync(x => x.Guid == f.PrimaryUnit.Guid)).Ativo);
        Assert.Equal(2, await f.Db.Setores.CountAsync());
        Assert.Equal(1, await f.Db.Funcionarios.CountAsync());
        Assert.Equal(link.Guid, (await f.Db.FuncionariosUnidadesAtuacao.SingleAsync()).Guid);
        await f.Service.ChangeHospitalUnitStatusAsync(f.PrimaryUnit.Guid, true, context, default);
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync(x => x.EntidadeGuid == f.PrimaryUnit.Guid));
        Assert.Equal(2, await f.Db.OutboxMessages.CountAsync());
        var audits = await f.Db.AuditLogs.ToListAsync();
        Assert.All(audits, a =>
        {
            Assert.Equal(context.CorrelationId, a.CorrelationId);
            Assert.Equal("trace-hospital", a.TraceId);
            Assert.Equal("127.0.0.1", a.Ip);
            Assert.NotNull(a.ValorAnterior);
            Assert.NotNull(a.ValorNovo);
        });
    }

    [Fact]
    public async Task OtherOrganizationAndMissingGuid_AreInvisibleToReadsAndWrites()
    {
        await using var f = await OrganizationCatalogFixture.CreateAsync();
        foreach (var guid in new[] { f.ForeignUnit.Guid, Guid.NewGuid() })
        {
            Assert.Null(await f.Service.GetHospitalUnitAsync(f.Actor.Guid, guid, default));
            Assert.Null(await f.Service.UpdateHospitalUnitAsync(guid, Request(), Context(f), default));
            Assert.Null(await f.Service.ChangeHospitalUnitStatusAsync(guid, false, Context(f), default));
        }
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
        var organizationGuid = await f.Db.Organizacoes.Where(x => x.Id == f.ForeignUnit.OrganizacaoId).Select(x => x.Guid).SingleAsync();
        await Assert.ThrowsAsync<OrganizationAccessDeniedException>(() => f.Service.CreateHospitalUnitAsync(Request() with { OrganizationGuid = organizationGuid }, Context(f), default));
        await Assert.ThrowsAsync<OrganizationAccessDeniedException>(() => f.Service.UpdateHospitalUnitAsync(f.PrimaryUnit.Guid, Request() with { OrganizationGuid = organizationGuid }, Context(f), default));
        Assert.Empty((await f.Service.ListHospitalUnitsAsync(f.Actor.Guid, new(null, null, OrganizationGuid: organizationGuid), default)).Items);
    }

    [Fact]
    public async Task UpdateKeepsOwnCnpj_RejectsCnpjOfAnotherUnit()
    {
        await using var f = await OrganizationCatalogFixture.CreateAsync();
        var first = await f.Service.CreateHospitalUnitAsync(Request() with { Cnpj = "11222333000181" }, Context(f), default);
        Assert.NotNull(await f.Service.UpdateHospitalUnitAsync(first.Guid, Request() with { Cnpj = "11.222.333/0001-81" }, Context(f), default));
        var second = await f.Service.CreateHospitalUnitAsync(Request("Outra") with { Cnpj = "04252011000110" }, Context(f), default);
        var error = await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() => f.Service.UpdateHospitalUnitAsync(second.Guid, Request("Outra") with { Cnpj = first.Cnpj }, Context(f), default));
        Assert.Equal("cnpj", error.Field);
        f.Db.ChangeTracker.Clear();
        Assert.Equal("04252011000110", (await f.Service.GetHospitalUnitAsync(f.Actor.Guid, second.Guid, default))!.Cnpj);
    }

    [Fact]
    public async Task InternalCodeIsScopedToOrganization_GlobalKeysAreNot()
    {
        await using var f = await OrganizationCatalogFixture.CreateAsync();
        await f.Service.CreateHospitalUnitAsync(Request() with { InternalCode = " HC ", Cnpj = "11222333000181", Cnes = "1234567" }, Context(f), default);
        var other = new UnidadeHospitalar(Guid.NewGuid(), f.ForeignUnit.OrganizacaoId,
            (Request("Externa") with { InternalCode = "hc" }).ToDomain(), f.Clock.GetUtcNow());
        f.Db.Add(other); await f.Db.SaveChangesAsync();
        Assert.Equal(2, await f.Db.UnidadesHospitalares.CountAsync(x => x.CodigoInterno == "HC"));
        var error = await Assert.ThrowsAsync<DuplicateBusinessKeyException>(() => f.Service.CreateHospitalUnitAsync(Request("Repetida") with { InternalCode = "hc" }, Context(f), default));
        Assert.Equal("internalCode", error.Field);
    }

    [Fact]
    public async Task FiltersAndPagination_AreAppliedBeforeSummaryProjection()
    {
        await using var f = await OrganizationCatalogFixture.CreateAsync();
        var created = await f.Service.CreateHospitalUnitAsync(Request() with { Cnpj = "11222333000181", Cnes = "1234567" }, Context(f), default);
        var matching = new OrganizationCatalogListQuery("Novo", true, 1, 1, "Hospitalar", "11.222.333/0001-81", "1234567", "São Paulo", "sp", f.Organization.Guid);
        var result = await f.Service.ListHospitalUnitsAsync(f.Actor.Guid, matching, default);
        Assert.Equal(created.Guid, Assert.Single(result.Items).Guid);
        Assert.Equal(1, result.Total);
        Assert.Empty((await f.Service.ListHospitalUnitsAsync(f.Actor.Guid, matching with { Page = 2 }, default)).Items);
        foreach (var query in new[] { matching with { LegalName = "ausente" }, matching with { Cnpj = "04252011000110" }, matching with { Cnes = "7654321" }, matching with { City = "Campinas" }, matching with { State = "RJ" }, matching with { Active = false }, matching with { Search = "inexistente" } })
            Assert.Equal(0, (await f.Service.ListHospitalUnitsAsync(f.Actor.Guid, query, default)).Total);
    }

    [Fact]
    public async Task SimilarityAlerts_DoNotBlockNamesOrExposeOtherOrganizations()
    {
        await using var f = await OrganizationCatalogFixture.CreateAsync();
        var first = await f.Service.CreateHospitalUnitAsync(Request("Hospital São Lucas"), Context(f), default);
        var query = new HospitalUnitDuplicateQuery("hospital sao lucas", null, "01001000", "10");
        Assert.Equal(first.Guid, Assert.Single(await f.Service.FindHospitalUnitDuplicatesAsync(f.Actor.Guid, query, default)).Guid);
        Assert.Empty(await f.Service.FindHospitalUnitDuplicatesAsync(f.Actor.Guid, query with { ExcludeGuid = first.Guid }, default));
        var second = await f.Service.CreateHospitalUnitAsync(Request("Hospital São Lucas"), Context(f), default);
        Assert.NotEqual(first.Guid, second.Guid);
        Assert.Empty(await f.Service.FindHospitalUnitDuplicatesAsync(f.Actor.Guid, query with { Number = "11" }, default));
    }

    [Fact]
    public async Task AuditDoesNotCopyResponsibleContactInformation()
    {
        await using var f = await OrganizationCatalogFixture.CreateAsync();
        await f.Service.CreateHospitalUnitAsync(Request() with { AdministrativeResponsibleName = "Pessoa privada", AdministrativeResponsibleEmail = "private@test.test" }, Context(f), default);
        var audit = await f.Db.AuditLogs.SingleAsync();
        Assert.DoesNotContain("Pessoa privada", audit.ValorNovo);
        Assert.DoesNotContain("private@test.test", audit.ValorNovo);
        Assert.DoesNotContain("private@test.test", (await f.Db.OutboxMessages.SingleAsync()).Payload);
    }
}
