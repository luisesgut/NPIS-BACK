using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using NpiManagement.Api.Contracts.Auth;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// GET /api/dashboard/summary contra SQL Server real (Fase 7). No asume una base vacía — otras
/// pruebas de esta colección comparten el mismo contenedor, así que sólo se afirma sobre datos
/// creados por esta prueba (nombres de cliente únicos), nunca sobre totales absolutos.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class DashboardEndpointTests(SqlServerFixture fixture) : IAsyncLifetime
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

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, AuthWebApplicationFactory.SeedPassword));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    [SkippableFact]
    public async Task Summary_reflects_a_closed_and_an_in_process_npi_for_an_admin()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var uniqueClient = $"Cliente Dashboard {Guid.NewGuid():N}";

        var comercial = await ClientAsync("lvega@bioflex.mx");
        var adminTotal = await ClientAsync("evelazquez@bioflex.mx");
        var adminIng = await ClientAsync("imartinez@bioflex.mx");

        // NPI 1: se queda en Draft (en proceso, GlobalTimer todavía no cierra).
        var draftResponse = await comercial.PostAsJsonAsync("/api/npis",
            new CreateNpiDraftRequest(new NpiCaptureDto { Cliente = uniqueClient }, Priority.Media, null));
        draftResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // NPI 2: se envía y se rechaza por no factibilidad — LifecycleStatus Rejected, GlobalTimer cerrado.
        var submittedResponse = await comercial.PostAsJsonAsync("/api/npis",
            new CreateNpiDraftRequest(new NpiCaptureDto { Cliente = uniqueClient }, Priority.Alta, null));
        var submittedId = await submittedResponse.Content.ReadFromJsonAsync<Guid>();
        (await comercial.PostAsync($"/api/npis/{submittedId}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var rejectResponse = await adminIng.PostAsJsonAsync($"/api/npis/{submittedId}/reject-no-feasibility", new NoteRequest("Sin factibilidad"));
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaryResponse = await adminTotal.GetAsync("/api/dashboard/summary");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<DashboardSummaryDto>();

        summary!.CountsByClient.Should().ContainKey(uniqueClient);
        summary.CountsByClient[uniqueClient].Should().Be(2);
        summary.CountsByLifecycleStatus.Should().ContainKey("Draft");
        summary.CountsByLifecycleStatus.Should().ContainKey("Rejected");
        summary.ClosedCount.Should().BeGreaterThanOrEqualTo(1);
        summary.InProcessCount.Should().BeGreaterThanOrEqualTo(1);
        summary.AverageGlobalDurationBusinessMs.Should().NotBeNull();
        summary.SlaCompliancePercent.Should().BeInRange(0, 100);
    }

    [SkippableFact]
    public async Task Summary_is_scoped_to_what_the_actor_can_access()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var uniqueClient = $"Cliente Dashboard Scope {Guid.NewGuid():N}";

        var comercial = await ClientAsync("lvega@bioflex.mx");
        var unrelatedDesigner = await ClientAsync("mparedes@bioflex.mx");

        var createResponse = await comercial.PostAsJsonAsync("/api/npis",
            new CreateNpiDraftRequest(new NpiCaptureDto { Cliente = uniqueClient }, Priority.Baja, null));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var summaryResponse = await unrelatedDesigner.GetAsync("/api/dashboard/summary");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<DashboardSummaryDto>();

        summary!.CountsByClient.Should().NotContainKey(uniqueClient);
    }
}
