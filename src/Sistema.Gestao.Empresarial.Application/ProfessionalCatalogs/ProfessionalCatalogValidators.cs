using FluentValidation;
using Sistema.Gestao.Empresarial.Domain.Pessoas;

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

public sealed class CreateProfessionalLevelRequestValidator : AbstractValidator<CreateProfessionalLevelRequest>
{
    public CreateProfessionalLevelRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(NivelProfissional.CodigoTamanhoMaximo);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NivelProfissional.NomeTamanhoMaximo);
        RuleFor(x => x.Order).InclusiveBetween(NivelProfissional.OrdemMinima, NivelProfissional.OrdemMaxima);
    }
}

public sealed class UpdateProfessionalLevelRequestValidator : AbstractValidator<UpdateProfessionalLevelRequest>
{
    public UpdateProfessionalLevelRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(NivelProfissional.CodigoTamanhoMaximo);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NivelProfissional.NomeTamanhoMaximo);
        RuleFor(x => x.Order).InclusiveBetween(NivelProfissional.OrdemMinima, NivelProfissional.OrdemMaxima);
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
