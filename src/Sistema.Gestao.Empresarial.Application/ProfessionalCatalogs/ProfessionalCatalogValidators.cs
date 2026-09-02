using FluentValidation;

namespace Sistema.Gestao.Empresarial.Application.ProfessionalCatalogs;

public sealed class ProfessionalCatalogListQueryValidator : AbstractValidator<ProfessionalCatalogListQuery>
{
    public ProfessionalCatalogListQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(150);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class CreateProfessionalCatalogRequestValidator
    : AbstractValidator<CreateProfessionalCatalogRequest>
{
    public CreateProfessionalCatalogRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateProfessionalCatalogRequestValidator
    : AbstractValidator<UpdateProfessionalCatalogRequest>
{
    public UpdateProfessionalCatalogRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
