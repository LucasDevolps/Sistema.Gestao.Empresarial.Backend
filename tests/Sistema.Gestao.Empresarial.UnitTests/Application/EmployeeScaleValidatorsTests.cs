using System.Text.Json;
using System.Text.Json.Nodes;
using Sistema.Gestao.Empresarial.Application.Employees;

namespace Sistema.Gestao.Empresarial.UnitTests.Application;

public sealed class EmployeeScaleValidatorsTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(-1, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(99, false)]
    public void Requests_ValidateProductivity(int productivity, bool valid)
    {
        var create = new CreateEmployeeRequest("Maria", "maria@test.com", null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 1, 1), [], []);
        var update = new UpdateEmployeeRequest(create.Name, create.Email, create.Phone,
            create.ProfessionGuid, create.PositionGuid, create.LevelGuid);
        Assert.Equal(valid, new CreateEmployeeRequestValidator().Validate(WithProductivity(create, productivity)).IsValid);
        Assert.Equal(valid, new UpdateEmployeeRequestValidator().Validate(WithProductivity(update, productivity)).IsValid);
    }

    private static T WithProductivity<T>(T request, int value)
    {
        var json = JsonSerializer.SerializeToNode(request)!;
        json["Productivity"] = value;
        return json.Deserialize<T>()!;
    }
}
