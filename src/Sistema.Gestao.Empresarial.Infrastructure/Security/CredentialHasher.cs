using Microsoft.AspNetCore.Identity;
using Sistema.Gestao.Empresarial.Application.Authentication;

namespace Sistema.Gestao.Empresarial.Infrastructure.Security;

public sealed class CredentialHasher : ICredentialHasher
{
    private static readonly object UserMarker = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(UserMarker, password);
    }

    public bool VerifyHashedPassword(string hash, string password)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        return _hasher.VerifyHashedPassword(UserMarker, hash, password) is not PasswordVerificationResult.Failed;
    }
}
