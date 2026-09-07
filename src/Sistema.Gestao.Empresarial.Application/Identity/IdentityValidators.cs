using FluentValidation;

namespace Sistema.Gestao.Empresarial.Application.Identity;

public sealed class IdentityListQueryValidator : AbstractValidator<IdentityListQuery>
{
    public IdentityListQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(254);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
