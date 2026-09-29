using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Employees;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Employees;

public sealed class EmployeeScaleTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task CreateAndRead_PersistScaleParameters(int productivity, bool participates)
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var request = fixture.CreateRequest(fixture.UnitB.Guid, [], []);
        var json = JsonSerializer.SerializeToNode(request)!;
        json["Productivity"] = productivity;
        json["ParticipatesInSchedule"] = participates;
        var context = fixture.Context(Guid.NewGuid());
        var created = await fixture.Service.CreateAsync(json.Deserialize<CreateEmployeeRequest>()!, context, default);
        fixture.Db.ChangeTracker.Clear();
        var found = await fixture.Service.GetAsync(created.Guid, context, default);
        var page = await fixture.Service.ListAsync(new EmployeeListQuery(null, null, null), context, default);
        AssertScale(created, productivity, participates);
        AssertScale(found!, productivity, participates);
        AssertScale(Assert.Single(page.Items, x => x.Guid == created.Guid), productivity, participates);
        var audit = await fixture.Db.AuditLogs.SingleAsync(x => x.CorrelationId == context.CorrelationId);
        var data = JsonNode.Parse(audit.ValorNovo!)!;
        Assert.Equal(productivity, data["productivity"]!.GetValue<int>());
        Assert.Equal(participates, data["participatesInSchedule"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Update_PreservesRelationshipsAndAuditsOnlyChanges()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var context = fixture.Context(Guid.NewGuid());
        var request = fixture.CreateRequest(fixture.UnitB.Guid,
            [new(fixture.UnitA.Guid, new DateOnly(2026, 1, 10))],
            [new(fixture.SectorA.Guid, new DateOnly(2026, 1, 15))]);
        var created = await fixture.Service.CreateAsync(request, context, default);
        AssertScale(created, 0, false);
        var update = new UpdateEmployeeRequest(request.Name, request.Email, request.Phone,
            request.ProfessionGuid, request.PositionGuid, request.LevelGuid, 2, true);
        var updated = await fixture.Service.UpdateAsync(created.Guid, update, context, default);
        AssertScale(updated!, 2, true);
        Assert.Equal(created.RegistrationNumber, updated!.RegistrationNumber);
        Assert.Equal(created.HiringUnit, updated.HiringUnit);
        Assert.Equal(created.AdmissionDate, updated.AdmissionDate);
        Assert.Equal(created.Profession, updated.Profession);
        Assert.Equal(created.Position, updated.Position);
        Assert.Equal(created.Level, updated.Level);
        Assert.Equal(created.Phone, updated.Phone);
        Assert.Equal(created.Active, updated.Active);
        Assert.Equal(created.ActingUnits, updated.ActingUnits);
        Assert.Equal(created.Sectors, updated.Sectors);
        var legacy = update with { Productivity = null, ParticipatesInSchedule = null };
        AssertScale((await fixture.Service.UpdateAsync(created.Guid, legacy, context, default))!, 2, true);
        await fixture.Service.UpdateAsync(created.Guid, update, context, default);
        var audit = await fixture.Db.AuditLogs.SingleAsync(x => x.Acao == "ATUALIZADO");
        Assert.Equal(0, JsonNode.Parse(audit.ValorAnterior!)!["productivity"]!.GetValue<int>());
        Assert.False(JsonNode.Parse(audit.ValorAnterior!)!["participatesInSchedule"]!.GetValue<bool>());
        Assert.Equal(2, JsonNode.Parse(audit.ValorNovo!)!["productivity"]!.GetValue<int>());
        Assert.True(JsonNode.Parse(audit.ValorNovo!)!["participatesInSchedule"]!.GetValue<bool>());
        Assert.Single(await fixture.Db.OutboxMessages.Where(x => x.EventType == "FuncionarioAtualizado").ToListAsync());
        var reset = await fixture.Service.UpdateAsync(created.Guid,
            update with { Productivity = 0, ParticipatesInSchedule = false }, context, default);
        AssertScale(reset!, 0, false);
    }

    [Fact]
    public async Task Filter_CombinesParticipationWithActiveStatusAndOrganization()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var context = fixture.Context(Guid.NewGuid());
        var request = fixture.CreateRequest(fixture.UnitB.Guid, [], []) with { Productivity = 1, ParticipatesInSchedule = true };
        var active = await fixture.Service.CreateAsync(request, context, default);
        var inactive = await fixture.Service.CreateAsync(request with { Email = "inactive@test.com" }, context, default);
        await fixture.Service.ChangeStatusAsync(inactive.Guid, false, context, default);
        var excluded = await fixture.Service.CreateAsync(request with { Email = "excluded@test.com", ParticipatesInSchedule = false }, context, default);
        fixture.Db.Funcionarios.Add(new Sistema.Gestao.Empresarial.Domain.Pessoas.Funcionario(
            Guid.NewGuid(), "External", "external@test.com", null,
            fixture.Profession.Id, fixture.Position.Id, fixture.Level.Id,
            fixture.OtherOrganizationUnit.Id, request.AdmissionDate, DateTimeOffset.UtcNow, 2, true));
        await fixture.Db.SaveChangesAsync();
        var participants = await fixture.Service.ListAsync(new(null, null, null, ParticipatesInSchedule: true), context, default);
        Assert.Equal(2, participants.Total);
        Assert.All(participants.Items, item => Assert.True(item.ParticipatesInSchedule));
        var eligible = await fixture.Service.ListAsync(new(null, true, null, ParticipatesInSchedule: true), context, default);
        Assert.Equal(active.Guid, Assert.Single(eligible.Items).Guid);
        var nonparticipants = await fixture.Service.ListAsync(new(null, null, null, ParticipatesInSchedule: false), context, default);
        Assert.Contains(nonparticipants.Items, x => x.Guid == excluded.Guid);
        Assert.All(nonparticipants.Items, item => Assert.False(item.ParticipatesInSchedule));
        var paged = await fixture.Service.ListAsync(new(null, null, null, 1, 1, true), context, default);
        Assert.Equal(2, paged.Total);
        Assert.Single(paged.Items);
        var inactiveResult = await fixture.Service.GetAsync(inactive.Guid, context, default);
        Assert.False(inactiveResult!.Active);
        Assert.True(inactiveResult.ParticipatesInSchedule);
    }
    private static void AssertScale<T>(T response, int productivity, bool participates)
    {
        var json = JsonSerializer.SerializeToNode(response)!;
        Assert.True(json.AsObject().ContainsKey("Productivity"));
        Assert.Equal(productivity, json["Productivity"]!.GetValue<int>());
        Assert.Equal(participates, json["ParticipatesInSchedule"]!.GetValue<bool>());
    }
}
