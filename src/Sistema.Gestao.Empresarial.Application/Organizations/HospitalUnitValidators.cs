using FluentValidation;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;

namespace Sistema.Gestao.Empresarial.Application.Organizations;

public sealed class HospitalUnitRegistrationRequestValidator : AbstractValidator<HospitalUnitRegistrationRequest>
{
    public HospitalUnitRegistrationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalName).MaximumLength(200);
        RuleFor(x => x.Cnpj).MaximumLength(18);
        RuleFor(x => x.Cnes).MaximumLength(7);
        RuleFor(x => x.InternalCode).MaximumLength(50);
        RuleFor(x => x.Acronym).MaximumLength(20);
        RuleFor(x => x.RegistrationStatus).MaximumLength(100);
        RuleFor(x => x.LegalNature).MaximumLength(200);
        RuleFor(x => x.PrimaryCnae).MaximumLength(200);
        RuleFor(x => x.SecondaryCnaes).MaximumLength(2000);
        RuleFor(x => x.StateRegistration).MaximumLength(50);
        RuleFor(x => x.MunicipalRegistration).MaximumLength(50);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(9);
        RuleFor(x => x.Street).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Number).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Complement).MaximumLength(150);
        RuleFor(x => x.District).NotEmpty().MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(2);
        RuleFor(x => x.State).Must(CadastroBrasileiro.UfValida).When(x => !string.IsNullOrWhiteSpace(x.State)).WithMessage("UF inválida.");
        RuleFor(x => x.IbgeCode).MaximumLength(7);
        RuleFor(x => x.Region).MaximumLength(30);
        RuleFor(x => x.AreaCode).MaximumLength(2);
        RuleFor(x => x.AddressReference).MaximumLength(300);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Phone).Must(CadastroBrasileiro.TelefoneValido).WithMessage("Telefone inválido.");
        RuleFor(x => x.SecondaryPhone).MaximumLength(30);
        RuleFor(x => x.SecondaryPhone).Must(CadastroBrasileiro.TelefoneValido).WithMessage("Telefone inválido.");
        RuleFor(x => x.Whatsapp).MaximumLength(30);
        RuleFor(x => x.Whatsapp).Must(CadastroBrasileiro.TelefoneValido).WithMessage("Telefone inválido.");
        RuleFor(x => x.Email).MaximumLength(254);
        RuleFor(x => x.Email).Must(CadastroBrasileiro.EmailValido).WithMessage("E-mail inválido.");
        RuleFor(x => x.AdministrativeEmail).MaximumLength(254);
        RuleFor(x => x.AdministrativeEmail).Must(CadastroBrasileiro.EmailValido).WithMessage("E-mail inválido.");
        RuleFor(x => x.Website).MaximumLength(500);
        RuleFor(x => x.Extension).MaximumLength(30);
        RuleFor(x => x.AdministrativeResponsibleName).MaximumLength(200);
        RuleFor(x => x.AdministrativeResponsibleRole).MaximumLength(150);
        RuleFor(x => x.AdministrativeResponsibleEmail).MaximumLength(254);
        RuleFor(x => x.AdministrativeResponsibleEmail).Must(CadastroBrasileiro.EmailValido).WithMessage("E-mail inválido.");
        RuleFor(x => x.AdministrativeResponsiblePhone).MaximumLength(30);
        RuleFor(x => x.AdministrativeResponsiblePhone).Must(CadastroBrasileiro.TelefoneValido).WithMessage("Telefone inválido.");
        RuleFor(x => x.TechnicalResponsibleName).MaximumLength(200);
        RuleFor(x => x.TechnicalResponsibleProfession).MaximumLength(150);
        RuleFor(x => x.TechnicalResponsibleCouncil).MaximumLength(50);
        RuleFor(x => x.TechnicalResponsibleCouncilNumber).MaximumLength(50);
        RuleFor(x => x.TechnicalResponsibleCouncilState).MaximumLength(2);
        RuleFor(x => x.TechnicalResponsibleCouncilState).Must(CadastroBrasileiro.UfValida).When(x => !string.IsNullOrWhiteSpace(x.TechnicalResponsibleCouncilState)).WithMessage("UF inválida.");
        RuleFor(x => x.TechnicalResponsibleEmail).MaximumLength(254);
        RuleFor(x => x.TechnicalResponsibleEmail).Must(CadastroBrasileiro.EmailValido).WithMessage("E-mail inválido.");
        RuleFor(x => x.TechnicalResponsiblePhone).MaximumLength(30);
        RuleFor(x => x.TechnicalResponsiblePhone).Must(CadastroBrasileiro.TelefoneValido).WithMessage("Telefone inválido.");
        RuleFor(x => x.ClinicalDirectorName).MaximumLength(200);
        RuleFor(x => x.ClinicalDirectorCrm).MaximumLength(50);
        RuleFor(x => x.ClinicalDirectorCrmState).MaximumLength(2);
        RuleFor(x => x.ClinicalDirectorCrmState).Must(CadastroBrasileiro.UfValida).When(x => !string.IsNullOrWhiteSpace(x.ClinicalDirectorCrmState)).WithMessage("UF inválida.");
        RuleFor(x => x.ClinicalDirectorEmail).MaximumLength(254);
        RuleFor(x => x.ClinicalDirectorEmail).Must(CadastroBrasileiro.EmailValido).WithMessage("E-mail inválido.");
        RuleFor(x => x.ClinicalDirectorPhone).MaximumLength(30);
        RuleFor(x => x.ClinicalDirectorPhone).Must(CadastroBrasileiro.TelefoneValido).WithMessage("Telefone inválido.");
        RuleFor(x => x.SanitaryPermit).MaximumLength(100);
        RuleFor(x => x.OperatingLicense).MaximumLength(100);
        RuleFor(x => x.RegulatoryNotes).MaximumLength(2000);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.OrganizationGuid).NotEqual(Guid.Empty).When(x => x.OrganizationGuid.HasValue);
        RuleFor(x => x.Cnpj).Must(CadastroBrasileiro.CnpjValido).When(x => !string.IsNullOrWhiteSpace(x.Cnpj)).WithMessage("CNPJ inválido.");
        RuleFor(x => x.Cnpj).NotEmpty().When(x => x.HasOwnCnpj);
        RuleFor(x => x.LegalName).NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.Cnpj));
        RuleFor(x => x.PostalCode).Must(CadastroBrasileiro.CepValido).WithMessage("CEP deve possuir 8 dígitos.");
        RuleFor(x => x.Cnes).Must(x => CadastroBrasileiro.Digitos(x!.Trim(), 7)).When(x => !string.IsNullOrWhiteSpace(x.Cnes)).WithMessage("CNES deve possuir 7 dígitos.");
        RuleFor(x => x.IbgeCode).Must(x => CadastroBrasileiro.Digitos(x!.Trim(), 7)).When(x => !string.IsNullOrWhiteSpace(x.IbgeCode));
        RuleFor(x => x.AreaCode).Must(x => CadastroBrasileiro.Digitos(x!.Trim(), 2)).When(x => !string.IsNullOrWhiteSpace(x.AreaCode));
        RuleFor(x => x.Website).Must(CadastroBrasileiro.SiteValido).WithMessage("Site deve ser uma URL HTTP ou HTTPS.");
        RuleFor(x => x.TotalBeds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IcuBeds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IcuBeds).LessThanOrEqualTo(x => x.TotalBeds).When(x => x.TotalBeds.HasValue && x.IcuBeds.HasValue);
        RuleFor(x => x.UnitType).IsInEnum();
        RuleFor(x => x.Nature).IsInEnum();
    }
}

public sealed class HospitalUnitDuplicateQueryValidator : AbstractValidator<HospitalUnitDuplicateQuery>
{
    public HospitalUnitDuplicateQueryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalName).MaximumLength(200);
        RuleFor(x => x.PostalCode).Must(CadastroBrasileiro.CepValido);
        RuleFor(x => x.Number).NotEmpty().MaximumLength(30);
        RuleFor(x => x.ExcludeGuid).NotEqual(Guid.Empty).When(x => x.ExcludeGuid.HasValue);
    }
}
