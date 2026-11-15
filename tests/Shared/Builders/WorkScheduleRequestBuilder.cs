using Sistema.Gestao.Empresarial.Application.WorkSchedules;

namespace Sistema.Gestao.Empresarial.Tests.Builders;

public sealed class WorkScheduleRequestBuilder
{
    private string _name = "6x1";
    private int _consecutiveWorkDays = 6;
    private int _restDays = 1;
    private int _maximumConsecutiveWorkDays = 6;
    private string? _description;

    public WorkScheduleRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public WorkScheduleRequestBuilder WithCycle(int consecutiveWorkDays, int restDays)
    {
        _consecutiveWorkDays = consecutiveWorkDays;
        _restDays = restDays;
        return this;
    }

    public WorkScheduleRequestBuilder WithConsecutiveWorkDays(int consecutiveWorkDays)
    {
        _consecutiveWorkDays = consecutiveWorkDays;
        return this;
    }

    public WorkScheduleRequestBuilder WithRestDays(int restDays)
    {
        _restDays = restDays;
        return this;
    }

    public WorkScheduleRequestBuilder WithMaximum(int maximumConsecutiveWorkDays)
    {
        _maximumConsecutiveWorkDays = maximumConsecutiveWorkDays;
        return this;
    }

    public WorkScheduleRequestBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public CreateWorkScheduleRequest BuildCreate() =>
        new(_name, _consecutiveWorkDays, _restDays, _maximumConsecutiveWorkDays, _description);

    public UpdateWorkScheduleRequest BuildUpdate() =>
        new(_name, _consecutiveWorkDays, _restDays, _maximumConsecutiveWorkDays, _description);
}
