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
