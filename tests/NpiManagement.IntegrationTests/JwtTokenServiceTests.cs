using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Auth;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// No requiere base de datos ni Docker — corre siempre. Verifica la forma del JWT emitido
/// (claims mínimos, expiración corta) y la generación/hash del refresh token.
/// </summary>
public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(TestClock clock) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "NpiManagement.Tests",
            Audience = "NpiManagement.Tests.Clients",
            SigningKey = "unit-test-signing-key-at-least-32-characters-long",
            AccessTokenMinutes = 15,
        }), clock);

    [Fact]
    public void CreateAccessToken_includes_the_minimum_required_claims()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(clock);
        var subject = new TokenSubject(Guid.NewGuid(), "user@bioflex.mx", "Usuario Prueba", "UsuarioComercial", AreaType.Comercial);

        var token = service.CreateAccessToken(subject);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == subject.UserId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == subject.Email);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == subject.FullName);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == subject.Role);
        jwt.Claims.Should().Contain(c => c.Type == "area" && c.Value == subject.Area.ToString());
        jwt.Issuer.Should().Be("NpiManagement.Tests");
        jwt.Audiences.Should().Contain("NpiManagement.Tests.Clients");
    }

    [Fact]
    public void CreateAccessToken_expires_after_the_configured_minutes()
    {
        var start = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(start);
        var service = CreateService(clock);
        var subject = new TokenSubject(Guid.NewGuid(), "user@bioflex.mx", "Usuario Prueba", "AdminTotal", AreaType.Total);

        var token = service.CreateAccessToken(subject);

        token.ExpiresAt.Should().Be(start.AddMinutes(15));
    }

    [Fact]
    public void CreateAccessToken_throws_when_signing_key_is_too_short()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "x",
            Audience = "y",
            SigningKey = "short",
        }), clock);
        var subject = new TokenSubject(Guid.NewGuid(), "a@b.com", "A", "UsuarioComercial", AreaType.Comercial);

        var act = () => service.CreateAccessToken(subject);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateAccessToken_marks_only_accounts_that_must_change_their_password()
    {
        var service = CreateService(new TestClock(DateTimeOffset.UtcNow));
        var pending = service.CreateAccessToken(new TokenSubject(Guid.NewGuid(), "a@bioflex.mx", "A", "UsuarioComercial", AreaType.Comercial, MustChangePassword: true));
        var normal = service.CreateAccessToken(new TokenSubject(Guid.NewGuid(), "b@bioflex.mx", "B", "UsuarioComercial", AreaType.Comercial));

        var handler = new JwtSecurityTokenHandler();
        handler.ReadJwtToken(pending.Value).Claims.Should().Contain(c => c.Type == AuthClaims.MustChangePassword);
        handler.ReadJwtToken(normal.Value).Claims.Should().NotContain(c => c.Type == AuthClaims.MustChangePassword);
    }

    [Fact]
    public void RefreshTokenGenerator_produces_unique_values_and_a_stable_hash()
    {
        var t1 = RefreshTokenGenerator.CreateToken();
        var t2 = RefreshTokenGenerator.CreateToken();

        t1.Should().NotBe(t2);
        RefreshTokenGenerator.Hash(t1).Should().Be(RefreshTokenGenerator.Hash(t1), "el hash debe ser determinista para el mismo valor");
        RefreshTokenGenerator.Hash(t1).Should().NotBe(RefreshTokenGenerator.Hash(t2));
        RefreshTokenGenerator.Hash(t1).Should().NotContain(t1, "el hash nunca debe contener el token original");
    }
}
