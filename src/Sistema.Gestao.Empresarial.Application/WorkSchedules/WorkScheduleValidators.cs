using FluentValidation;
using Sistema.Gestao.Empresarial.Domain.Escalas;

namespace Sistema.Gestao.Empresarial.Application.WorkSchedules;

public sealed class WorkScheduleListQueryValidator : AbstractValidator<WorkScheduleListQuery>
{
    public WorkScheduleListQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(JornadaTrabalho.NomeTamanhoMaximo);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public static class WorkScheduleRules
{
    public const string CycleExceedsMaximumMessage =
        "Os dias consecutivos de trabalho não podem ser maiores que o máximo de dias consecutivos permitido.";

    /// <summary>
    /// Só compara ciclo e máximo quando ambos já estão na faixa válida; caso
    /// contrário, a regra de faixa do próprio campo já reporta o erro.
    /// </summary>
    public static bool BothInRange(int consecutiveWorkDays, int maximumConsecutiveWorkDays) =>
        consecutiveWorkDays is >= 1 and <= JornadaTrabalho.MaximoDiasConsecutivosPermitido
        && maximumConsecutiveWorkDays is >= 1 and <= JornadaTrabalho.MaximoDiasConsecutivosPermitido;
}

public sealed class CreateWorkScheduleRequestValidator : AbstractValidator<CreateWorkScheduleRequest>
{
    public CreateWorkScheduleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(JornadaTrabalho.NomeTamanhoMaximo);
        RuleFor(x => x.ConsecutiveWorkDays)
            .InclusiveBetween(1, JornadaTrabalho.MaximoDiasConsecutivosPermitido);
        RuleFor(x => x.ConsecutiveWorkDays)
            .LessThanOrEqualTo(x => x.MaximumConsecutiveWorkDays)
            .When(x => WorkScheduleRules.BothInRange(x.ConsecutiveWorkDays, x.MaximumConsecutiveWorkDays))
            .WithMessage(WorkScheduleRules.CycleExceedsMaximumMessage);
        RuleFor(x => x.RestDays).GreaterThan(0);
        RuleFor(x => x.MaximumConsecutiveWorkDays)
            .InclusiveBetween(1, JornadaTrabalho.MaximoDiasConsecutivosPermitido);
        RuleFor(x => x.Description).MaximumLength(JornadaTrabalho.DescricaoTamanhoMaximo);
    }
}

public sealed class UpdateWorkScheduleRequestValidator : AbstractValidator<UpdateWorkScheduleRequest>
{
    public UpdateWorkScheduleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(JornadaTrabalho.NomeTamanhoMaximo);
        RuleFor(x => x.ConsecutiveWorkDays)
            .InclusiveBetween(1, JornadaTrabalho.MaximoDiasConsecutivosPermitido);
        RuleFor(x => x.ConsecutiveWorkDays)
            .LessThanOrEqualTo(x => x.MaximumConsecutiveWorkDays)
            .When(x => WorkScheduleRules.BothInRange(x.ConsecutiveWorkDays, x.MaximumConsecutiveWorkDays))
            .WithMessage(WorkScheduleRules.CycleExceedsMaximumMessage);
        RuleFor(x => x.RestDays).GreaterThan(0);
        RuleFor(x => x.MaximumConsecutiveWorkDays)
            .InclusiveBetween(1, JornadaTrabalho.MaximoDiasConsecutivosPermitido);
        RuleFor(x => x.Description).MaximumLength(JornadaTrabalho.DescricaoTamanhoMaximo);
    }
}
