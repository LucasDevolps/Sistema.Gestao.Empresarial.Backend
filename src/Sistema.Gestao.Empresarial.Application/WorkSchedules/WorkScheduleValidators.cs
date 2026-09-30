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

public sealed class CreateWorkScheduleRequestValidator : AbstractValidator<CreateWorkScheduleRequest>
{
    public CreateWorkScheduleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(JornadaTrabalho.NomeTamanhoMaximo);
        RuleFor(x => x.ConsecutiveWorkDays).GreaterThan(0);
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
        RuleFor(x => x.ConsecutiveWorkDays).GreaterThan(0);
        RuleFor(x => x.RestDays).GreaterThan(0);
        RuleFor(x => x.MaximumConsecutiveWorkDays)
            .InclusiveBetween(1, JornadaTrabalho.MaximoDiasConsecutivosPermitido);
        RuleFor(x => x.Description).MaximumLength(JornadaTrabalho.DescricaoTamanhoMaximo);
    }
}
