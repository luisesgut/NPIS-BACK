using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NpiManagement.Api.Contracts.Auth;
using NpiManagement.Api.Contracts.Users;
using NpiManagement.Application.Abstractions;
using Xunit;

namespace NpiManagement.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class PasswordEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    // Cuentas semilla que ninguna otra prueba usa: cambiarles la contraseña no rompe el resto de la colección.
    private const string ResetTarget = "testevez@bioflex.mx";
    private const string SelfServiceUser = "gquesada@bioflex.mx";
    private const string AdminTotal = "evelazquez@bioflex.mx";
    private const string AreaAdmin = "bbeas@bioflex.mx";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

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

    private async Task<HttpResponseMessage> LoginResponseAsync(string email, string password) =>
        await _factory!.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    private async Task<(HttpClient Client, AuthResponse Auth)> LoginAsync(string email, string password)
    {
        var response = await LoginResponseAsync(email, password);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;

        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static async Task<Guid> UserIdAsync(HttpClient admin, string email)
    {
        var users = await admin.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", Json);
        return users!.Single(u => u.Email == email).Id;
    }

    [SkippableFact]
    public async Task A_reset_password_replaces_the_old_one_and_must_be_changed_before_using_the_system()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var (admin, _) = await LoginAsync(AdminTotal, AuthWebApplicationFactory.SeedPassword);
        var targetId = await UserIdAsync(admin, ResetTarget);

        var reset = await admin.PostAsync($"/api/users/{targetId}/reset-password", null);
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        var temporary = (await reset.Content.ReadFromJsonAsync<ResetPasswordResponse>(Json))!.TemporaryPassword;

        (await LoginResponseAsync(ResetTarget, AuthWebApplicationFactory.SeedPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (target, auth) = await LoginAsync(ResetTarget, temporary);
        auth.User.MustChangePassword.Should().BeTrue();
        (await target.GetAsync("/api/npis")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "con contraseña temporal sólo se permite /api/auth");

        var change = await target.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(temporary, "Nueva-Clave#2026"));
        change.StatusCode.Should().Be(HttpStatusCode.OK);
        var renewed = (await change.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        renewed.User.MustChangePassword.Should().BeFalse();

        var afterChange = _factory!.CreateClient();
        afterChange.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renewed.AccessToken);
        (await afterChange.GetAsync("/api/npis")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Only_admin_total_can_reset_passwords_and_never_their_own()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var (admin, adminAuth) = await LoginAsync(AdminTotal, AuthWebApplicationFactory.SeedPassword);
        var targetId = await UserIdAsync(admin, ResetTarget);

        var (areaAdmin, _) = await LoginAsync(AreaAdmin, AuthWebApplicationFactory.SeedPassword);
        (await areaAdmin.PostAsync($"/api/users/{targetId}/reset-password", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await admin.PostAsync($"/api/users/{adminAuth.User.Id}/reset-password", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Changing_your_own_password_requires_the_current_one()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var (client, _) = await LoginAsync(SelfServiceUser, AuthWebApplicationFactory.SeedPassword);

        var wrong = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest("no-es-la-actual", "Otra-Clave#2026"));
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var right = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(AuthWebApplicationFactory.SeedPassword, "Otra-Clave#2026"));
        right.StatusCode.Should().Be(HttpStatusCode.OK);

        (await LoginResponseAsync(SelfServiceUser, "Otra-Clave#2026")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
