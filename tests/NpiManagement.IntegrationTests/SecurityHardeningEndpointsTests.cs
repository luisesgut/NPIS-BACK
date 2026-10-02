using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NpiManagement.Api.Contracts.Auth;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// Endurecimiento de Fase 7: encabezados de seguridad presentes en toda respuesta, y límite de
/// tasa por IP en /api/auth/login (defensa adicional al Lockout por cuenta de Identity — ver
/// Program.cs). Contra SQL Server real (Testcontainers) porque requiere el host completo.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SecurityHardeningEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private AuthWebApplicationFactory? _factory;

    public Task InitializeAsync()
    {
        if (!fixture.IsAvailable) return Task.CompletedTask;
        _factory = new AuthWebApplicationFactory(fixture.ConnectionString, AuthWebApplicationFactory.SeedPassword);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task Responses_include_hardening_headers()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = _factory!.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
    }

    [SkippableFact]
    public async Task Repeated_login_attempts_from_the_same_client_are_eventually_rate_limited()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = _factory!.CreateClient();

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 15 && lastStatus != HttpStatusCode.TooManyRequests; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nadie@bioflex.mx", "credencial-incorrecta"));
            lastStatus = response.StatusCode;
        }

        lastStatus.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
