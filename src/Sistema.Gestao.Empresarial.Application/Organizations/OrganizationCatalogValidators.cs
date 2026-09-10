using FluentValidation;

namespace Sistema.Gestao.Empresarial.Application.Organizations;

public sealed class OrganizationCatalogListQueryValidator
    : AbstractValidator<OrganizationCatalogListQuery>
{
    public OrganizationCatalogListQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class SectorListQueryValidator : AbstractValidator<SectorListQuery>
{
    public SectorListQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(150);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class SectorCategoryListQueryValidator : AbstractValidator<SectorCategoryListQuery>
{
    public SectorCategoryListQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(120);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class CreateSectorServedUnitRequestValidator
    : AbstractValidator<CreateSectorServedUnitRequest>
{
    public CreateSectorServedUnitRequestValidator()
    {
        RuleFor(x => x.UnitGuid).NotEmpty();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
    }
}

public sealed class CreateSectorRequestValidator : AbstractValidator<CreateSectorRequest>
{
    public const int MaximumServedUnits = 50;

    public CreateSectorRequestValidator()
    {
        RuleFor(x => x.UnitGuid).NotEmpty();
        RuleFor(x => x.CategoryGuid).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Sigla).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.InternalLocation).MaximumLength(150);
        RuleFor(x => x.Extension).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(254).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleForEach(x => x.ServedUnits).SetValidator(new CreateSectorServedUnitRequestValidator());
        RuleFor(x => x.ServedUnits)
            .Must(items => items is null || items.Count <= MaximumServedUnits)
            .WithMessage($"Informe no máximo {MaximumServedUnits} unidades atendidas.");
        RuleFor(x => x.ServedUnits)
            .Must(HaveDistinctUnits)
            .WithMessage("Uma unidade atendida não pode ser informada mais de uma vez.");
        RuleFor(x => x.ServedUnits)
            .Must((request, items) => items is null || items.All(item => item.UnitGuid != request.UnitGuid))
            .WithMessage("A unidade principal não deve ser informada como unidade atendida.");
        RuleFor(x => x.ServedUnits)
            .Must(items => items is null || items.Count == 0)
            .When(x => !x.AllowsSharedActing)
            .WithMessage("Habilite a atuação compartilhada para informar unidades atendidas.");
    }

    private static bool HaveDistinctUnits(IReadOnlyCollection<CreateSectorServedUnitRequest>? items) =>
        items is null || items.Select(x => x.UnitGuid).Distinct().Count() == items.Count;
}

public sealed class UpdateSectorRequestValidator : AbstractValidator<UpdateSectorRequest>
{
    public UpdateSectorRequestValidator()
    {
        RuleFor(x => x.CategoryGuid).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Sigla).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.InternalLocation).MaximumLength(150);
        RuleFor(x => x.Extension).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(254).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class AddSectorServedUnitRequestValidator : AbstractValidator<AddSectorServedUnitRequest>
{
    public AddSectorServedUnitRequestValidator()
    {
        RuleFor(x => x.UnitGuid).NotEmpty();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
    }
}

public sealed class EndSectorServedUnitRequestValidator : AbstractValidator<EndSectorServedUnitRequest>
{
    public EndSectorServedUnitRequestValidator() =>
        RuleFor(x => x.EndDate).NotEqual(default(DateOnly));
}

public sealed class CreateSectorCategoryRequestValidator : AbstractValidator<CreateSectorCategoryRequest>
{
    public CreateSectorCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateSectorCategoryRequestValidator : AbstractValidator<UpdateSectorCategoryRequest>
{
    public UpdateSectorCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
