using System.Net.Mail;
using Sistema.Gestao.Empresarial.Domain.Common;

namespace Sistema.Gestao.Empresarial.Domain.Organizacoes;

/// <summary>Normalização e validação compartilhadas pelo domínio, requests e consultas auxiliares.</summary>
public static class CadastroBrasileiro
{
    public static string SemMascaraCnpj(string? value) =>
        (value?.Trim() ?? string.Empty).Replace(".", "").Replace("/", "").Replace("-", "");

    public static string SemMascaraCep(string? value) => (value?.Trim() ?? string.Empty).Replace("-", "");

    public static bool Digitos(string value, int length) =>
        value.Length == length && value.All(char.IsAsciiDigit);

    public static bool CnpjValido(string? value)
    {
        var digits = SemMascaraCnpj(value);
        if (!Digitos(digits, 14) || digits.All(x => x == digits[0])) return false;
        for (var size = 12; size <= 13; size++)
        {
            var sum = 0;
            var weight = size - 7;
            for (var i = 0; i < size; i++)
            {
                sum += (digits[i] - '0') * weight;
                if (--weight < 2) weight = 9;
            }
            var remainder = sum % 11;
            if (digits[size] - '0' != (remainder < 2 ? 0 : 11 - remainder)) return false;
        }
        return true;
    }

    public static string? NormalizarCnpj(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!CnpjValido(value)) throw new DomainException("CNPJ inválido.");
        return SemMascaraCnpj(value);
    }

    public static bool CepValido(string? value) => Digitos(SemMascaraCep(value), 8);

    public static string NormalizarCep(string? value) => CepValido(value)
        ? SemMascaraCep(value) : throw new DomainException("CEP deve possuir 8 dígitos.");

    public static bool UfValida(string? value) => value?.Trim().ToUpperInvariant() is
        "AC" or "AL" or "AP" or "AM" or "BA" or "CE" or "DF" or "ES" or "GO" or "MA" or "MT" or "MS"
        or "MG" or "PA" or "PB" or "PR" or "PE" or "PI" or "RJ" or "RN" or "RS" or "RO" or "RR" or "SC" or "SP" or "SE" or "TO";

    public static bool EmailValido(string? value) => string.IsNullOrWhiteSpace(value)
        || MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();

    public static bool TelefoneValido(string? value) => string.IsNullOrWhiteSpace(value)
        || value.All(c => char.IsAsciiDigit(c) || c is '+' or '(' or ')' or '-' or '.' or ' ')
            && value.Count(char.IsAsciiDigit) is >= 8 and <= 15;

    public static bool SiteValido(string? value) => string.IsNullOrWhiteSpace(value)
        || Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && !string.IsNullOrWhiteSpace(uri.Host);
}
