namespace Sistema.Gestao.Empresarial.Domain.Common;

internal static class Guard
{
    public static string TextoObrigatorio(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{field} é obrigatório.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{field} deve possuir no máximo {maxLength} caracteres.");
        }

        return normalized;
    }
}
