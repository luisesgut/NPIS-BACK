using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NpiManagement.Api.Contracts.Auth;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// Flujo completo de autenticación contra la API real (Program.cs) y un SQL Server real —
/// login, bloqueo por intentos fallidos, rotación de refresh token, detección de reuso y logout.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AuthEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string SeededEmail = "lvega@bioflex.mx"; // UsuarioComercial, ver DevelopmentDataSeeder
    private AuthWebApplicationFactory? _factory;
    private HttpClient? _client;

    public Task InitializeAsync()
    {
        if (!fixture.IsAvailable) return Task.CompletedTask;

        _factory = new AuthWebApplicationFactory(fixture.ConnectionString, AuthWebApplicationFactory.SeedPassword);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task Login_with_valid_credentials_returns_access_and_refresh_tokens()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var response = await _client!.PostAsJsonAsync("/api/auth/login", new LoginRequest(SeededEmail, AuthWebApplicationFactory.SeedPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.User.Email.Should().Be(SeededEmail);
        body.User.Role.Should().Be("UsuarioComercial");
    }

    [SkippableFact]
    public async Task Login_with_wrong_password_returns_401()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var response = await _client!.PostAsJsonAsync("/api/auth/login", new LoginRequest(SeededEmail, "wrong-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Login_with_unknown_email_returns_401_not_404()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var response = await _client!.PostAsJsonAsync("/api/auth/login", new LoginRequest("nadie@bioflex.mx", "cualquier-cosa"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "no debe revelar si el correo existe o no");
    }

    [SkippableFact]
    public async Task Me_requires_authentication_and_reflects_the_token_claims()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var anonymous = await _client!.GetAsync("/api/auth/me");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var login = await _client!.PostAsJsonAsync("/api/auth/login", new LoginRequest(SeededEmail, AuthWebApplicationFactory.SeedPassword));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var authenticated = await _client!.SendAsync(request);

        authenticated.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await authenticated.Content.ReadFromJsonAsync<UserResponse>();
        me!.Email.Should().Be(SeededEmail);
    }

    [SkippableFact]
    public async Task Refresh_rotates_the_token_and_the_previous_one_stops_working()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var login = await _client!.PostAsJsonAsync("/api/auth/login", new LoginRequest(SeededEmail, AuthWebApplicationFactory.SeedPassword));
        var original = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        var refreshed = await _client!.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(original.RefreshToken));
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!;
        newTokens.RefreshToken.Should().NotBe(original.RefreshToken);

        var reuseOld = await _client!.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(original.RefreshToken));
        reuseOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "el refresh token original ya fue rotado y no debe volver a funcionar");
    }

    [SkippableFact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_entire_session_chain()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var login = await _client!.PostAsJsonAsync("/api/auth/login", new LoginRequest(SeededEmail, AuthWebApplicationFactory.SeedPassword));
        var original = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        var rotated = await _client!.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(original.RefreshToken));
        var rotatedTokens = (await rotated.Content.ReadFromJsonAsync<AuthResponse>())!;

        // Reusar el token original (ya rotado) — indicio de robo: debe invalidar también el nuevo.
        await _client!.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(original.RefreshToken));

        var tryNewToken = await _client!.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(rotatedTokens.RefreshToken));
        tryNewToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "el reuso de un token rotado debe revocar toda la cadena de sesión, incluido el token vigente");
    }

    [SkippableFact]
    public async Task Logout_revokes_the_refresh_token()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var login = await _client!.PostAsJsonAsync("/api/auth/login", new LoginRequest(SeededEmail, AuthWebApplicationFactory.SeedPassword));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        var logout = await _client!.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(tokens.RefreshToken));
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var tryRefresh = await _client!.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        tryRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Repeated_failed_logins_lock_the_account()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        const string emailToLock = "sgomez@bioflex.mx"; // usuario semilla distinto, para no afectar otras pruebas

        for (var i = 0; i < 5; i++)
            await _client!.PostAsJsonAsync("/api/auth/login", new LoginRequest(emailToLock, "wrong-password"));

        // Sexto intento, incluso con la contraseña CORRECTA, debe seguir bloqueado.
        var response = await _client!.PostAsJsonAsync("/api/auth/login", new LoginRequest(emailToLock, AuthWebApplicationFactory.SeedPassword));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
