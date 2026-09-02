using FluentValidation;

namespace Sistema.Gestao.Empresarial.Application.Employees;

public sealed class EmployeeListQueryValidator : AbstractValidator<EmployeeListQuery>
{
    public EmployeeListQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.ProfessionGuid).NotEmpty();
        RuleFor(x => x.PositionGuid).NotEmpty();
        RuleFor(x => x.LevelGuid).NotEmpty();
        RuleFor(x => x.HiringUnitGuid).NotEmpty();
        RuleFor(x => x.AdmissionDate).NotEqual(default(DateOnly));
        RuleForEach(x => x.ActingUnits).SetValidator(new CreateEmployeeActingUnitRequestValidator());
        RuleForEach(x => x.Sectors).SetValidator(new CreateEmployeeSectorRequestValidator());
        RuleFor(x => x.ActingUnits)
            .Must(HaveDistinctActingUnits)
            .WithMessage("Uma unidade de atuação não pode ser informada mais de uma vez.");
        RuleFor(x => x.Sectors)
            .Must(HaveDistinctSectors)
            .WithMessage("Um setor não pode ser informado mais de uma vez.");
    }

    private static bool HaveDistinctActingUnits(IReadOnlyCollection<CreateEmployeeActingUnitRequest>? items) =>
        items is null || items.Select(x => x.UnitGuid).Distinct().Count() == items.Count;

    private static bool HaveDistinctSectors(IReadOnlyCollection<CreateEmployeeSectorRequest>? items) =>
        items is null || items.Select(x => x.SectorGuid).Distinct().Count() == items.Count;
}

public sealed class CreateEmployeeActingUnitRequestValidator : AbstractValidator<CreateEmployeeActingUnitRequest>
{
    public CreateEmployeeActingUnitRequestValidator()
    {
        RuleFor(x => x.UnitGuid).NotEmpty();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
    }
}

public sealed class CreateEmployeeSectorRequestValidator : AbstractValidator<CreateEmployeeSectorRequest>
{
    public CreateEmployeeSectorRequestValidator()
    {
        RuleFor(x => x.SectorGuid).NotEmpty();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
    }
}

public sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.ProfessionGuid).NotEmpty();
        RuleFor(x => x.PositionGuid).NotEmpty();
        RuleFor(x => x.LevelGuid).NotEmpty();
    }
}

public sealed class AddEmployeeActingUnitRequestValidator : AbstractValidator<AddEmployeeActingUnitRequest>
{
    public AddEmployeeActingUnitRequestValidator()
    {
        RuleFor(x => x.UnitGuid).NotEmpty();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
    }
}

public sealed class AddEmployeeSectorRequestValidator : AbstractValidator<AddEmployeeSectorRequest>
{
    public AddEmployeeSectorRequestValidator()
    {
        RuleFor(x => x.SectorGuid).NotEmpty();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
    }
}

public sealed class EndEmployeeRelationshipRequestValidator : AbstractValidator<EndEmployeeRelationshipRequest>
{
    public EndEmployeeRelationshipRequestValidator() =>
        RuleFor(x => x.EndDate).NotEqual(default(DateOnly));
}
