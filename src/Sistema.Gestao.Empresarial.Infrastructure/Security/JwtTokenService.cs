using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Infrastructure.Configuration;

namespace Sistema.Gestao.Empresarial.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public TokenMaterial Create(Guid userGuid, Guid sessionId, long sessionVersion, DateTimeOffset now)
    {
        var jti = Guid.NewGuid().ToString("N");
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userGuid.ToString("D"),
                [JwtRegisteredClaimNames.Sid] = sessionId.ToString("D"),
                [JwtRegisteredClaimNames.Jti] = jti,
                ["session_version"] = sessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);
        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return new TokenMaterial(
            accessToken,
            refreshToken,
            HashToken(accessToken),
            HashToken(refreshToken),
            jti,
            expiresAt);
    }

    public string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
