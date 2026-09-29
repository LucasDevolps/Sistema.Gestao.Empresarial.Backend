namespace Sistema.Gestao.Empresarial.Application.Organizations;

public enum LookupOutcome { Found, NotFound, InvalidInput, Unavailable }

/// <summary>Resultado auxiliar; indisponibilidade nunca é uma restrição ao cadastro manual.</summary>
public sealed record LookupResult<T>(LookupOutcome Outcome, T? Data = default);

/// <summary>Atividade econômica informada pelo provedor.</summary>
public sealed record CnaeLookupResponse(string? Code, string? Description);

/// <summary>Dados sugeridos pela BrasilAPI, sujeitos a confirmação/edição antes de gravar.</summary>
public sealed record CnpjLookupResponse(
    string Cnpj, string LegalName, string? Name, string? RegistrationStatus, DateOnly? OpeningDate,
    string? LegalNature, CnaeLookupResponse PrimaryCnae, IReadOnlyCollection<CnaeLookupResponse> SecondaryCnaes,
    string? Phone, string? Email, string? PostalCode, string? Street, string? Number, string? Complement,
    string? District, string? City, string? State, bool InactiveRegistrationWarning);

/// <summary>Endereço sugerido pelo ViaCEP. Número não é retornado; preenchimento é manual.</summary>
public sealed record CepLookupResponse(
    string PostalCode, string? Street, string? Complement, string? District, string City,
    string State, string? IbgeCode, string? AreaCode, string? Region);

public interface ICnpjLookupService
{
    Task<LookupResult<CnpjLookupResponse>> LookupAsync(string cnpj, CancellationToken cancellationToken);
}

public interface ICepLookupService
{
    Task<LookupResult<CepLookupResponse>> LookupAsync(string cep, CancellationToken cancellationToken);
}
