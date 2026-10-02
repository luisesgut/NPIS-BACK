using System.Security.Cryptography;

namespace NpiManagement.Infrastructure.Auth;

/// <summary>
/// Genera el valor aleatorio del refresh token (nunca persistido tal cual) y su hash SHA-256
/// (lo único que se guarda en base de datos) — ver docs/architecture-decisions.md ADR-019.
/// </summary>
public static class RefreshTokenGenerator
{
    public static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlTextEncoder.Encode(bytes);
    }

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

/// <summary>Codificación base64url mínima (sin dependencias externas) para el valor del refresh token.</summary>
internal static class Base64UrlTextEncoder
{
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
