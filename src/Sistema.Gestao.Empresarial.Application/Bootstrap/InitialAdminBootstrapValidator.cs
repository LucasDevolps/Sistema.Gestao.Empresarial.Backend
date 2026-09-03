using FluentValidation;

namespace Sistema.Gestao.Empresarial.Application.Bootstrap;

public sealed class InitialAdminBootstrapRequestValidator : AbstractValidator<InitialAdminBootstrapRequest>
{
    public InitialAdminBootstrapRequestValidator()
    {
        RuleFor(x => x.OrganizationName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HospitalUnitName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProfessionName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PositionName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ProfessionalLevelCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.AdministratorName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdministratorEmail).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.AdministratorPhone).MaximumLength(30);
        RuleFor(x => x.AdmissionDate).NotEmpty();
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(16)
            .MaximumLength(500)
            .Must(password => password.Any(char.IsUpper))
            .WithMessage("A senha deve conter ao menos uma letra maiúscula.")
            .Must(password => password.Any(char.IsLower))
            .WithMessage("A senha deve conter ao menos uma letra minúscula.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("A senha deve conter ao menos um número.")
            .Must(password => password.Any(character => !char.IsLetterOrDigit(character)))
            .WithMessage("A senha deve conter ao menos um caractere especial.");
    }
}
