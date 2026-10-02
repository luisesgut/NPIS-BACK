using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain.Common;

namespace NpiManagement.Infrastructure.Auth;

/// <summary>
/// Emite access tokens JWT firmados (HMAC-SHA256). Claims mínimos: sub, email, name, role
/// (ClaimTypes.Role, para que [Authorize(Roles=...)] funcione sin mapeo adicional) y "area"
/// (claim propio). El signing key sale de configuración/User Secrets — nunca hardcodeado.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(TokenSubject subject)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey debe estar configurada con al menos 32 caracteres (User Secrets/variables de entorno — nunca en el repositorio).");

        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, subject.Email),
            new(ClaimTypes.Name, subject.FullName),
            new(ClaimTypes.Role, subject.Role),
            new("area", subject.Area.ToString()),
        };

        // Sólo se agrega cuando aplica: su mera presencia es lo que la API revisa para bloquear.
        if (subject.MustChangePassword)
            claims.Add(new Claim(AuthClaims.MustChangePassword, "true"));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(value, expiresAt);
    }
}
