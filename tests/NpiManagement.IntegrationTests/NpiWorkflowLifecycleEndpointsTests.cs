using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using NpiManagement.Api.Contracts.Auth;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// Recorre el ciclo completo del NPI (Draft → Active → Completed) llamando a la API real vía
/// HTTP, con SQL Server real (Testcontainers) — la prueba de extremo a extremo definitiva.
/// Complementa (no duplica) NpiManagement.Application.Tests.Npis.FullWorkflowLifecycleTests,
/// que recorre el mismo camino sin HTTP ni base de datos.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class NpiWorkflowLifecycleEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
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
    public async Task Full_lifecycle_reaches_Completed_through_real_HTTP_calls()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var comercial = await ClientAsync("lvega@bioflex.mx");
        var adminIng = await ClientAsync("imartinez@bioflex.mx");
        var adminDis = await ClientAsync("schavez@bioflex.mx");
        var ingeniero = await ClientAsync("zgonzalez@bioflex.mx");
        var disenador = await ClientAsync("mparedes@bioflex.mx");

        // 1) Crear + enviar
        var createResponse = await comercial.PostAsJsonAsync("/api/npis",
            new CreateNpiDraftRequest(new NpiCaptureDto { Cliente = "Cliente Lifecycle", NumeroCotizacion = "1001" }, Priority.Alta, null));
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();

        (await comercial.PostAsJsonAsync($"/api/npis/{id}/links", new AddLinkRequest("https://example.com/artes.pdf", "Artes"))).EnsureSuccessStatusCode();
        var submitResponse = await comercial.PostAsync($"/api/npis/{id}/submit", null);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterSubmit = await submitResponse.Content.ReadFromJsonAsync<NpiDetailDto>();
        afterSubmit!.NpiNumber.Should().NotBeNullOrEmpty();
        afterSubmit.LifecycleStatus.Should().Be("Active");

        // 2) Asignaciones
        var ingUser = await GetUserIdAsync(adminIng, "zgonzalez@bioflex.mx");
        var disUser = await GetUserIdAsync(adminDis, "mparedes@bioflex.mx");
        (await adminIng.PostAsJsonAsync($"/api/npis/{id}/assign-engineering", new AssignUserRequest(ingUser))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await adminDis.PostAsJsonAsync($"/api/npis/{id}/assign-design", new AssignDesignRequest(disUser, ArtClassification.C, false))).StatusCode.Should().Be(HttpStatusCode.OK);

        // 3) Factibilidades independientes + Artes Validadas
        (await disenador.PostAsJsonAsync($"/api/npis/{id}/design-feasibility", new DesignFeasibilityRequest("Ok", ArtClassification.A, null))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ingeniero.PostAsJsonAsync($"/api/npis/{id}/engineering-feasibility", new NoteRequest("Ok"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await disenador.PostAsync($"/api/npis/{id}/validate-artwork", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 4) PrintCard: Diseño genera (queda aprobada por Diseño) → Ingeniería → cliente
        var printCardFile = await (await disenador.PostAsJsonAsync($"/api/npis/{id}/links", new AddLinkRequest("https://example.com/pc.pdf", "PrintCard", AttachmentContext.PrintCard))).Content.ReadFromJsonAsync<AttachmentDto>();
        (await disenador.PostAsJsonAsync($"/api/npis/{id}/printcards", new CreatePrintCardVersionRequest("PC-LC-001", printCardFile!.Id, null))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ingeniero.PostAsJsonAsync($"/api/npis/{id}/printcard-approvals", new ApprovePrintCardRequest(ApproverRole.Engineering, null, null))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await comercial.PostAsync($"/api/npis/{id}/send-printcard-to-client", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var clientEvidence = await (await comercial.PostAsJsonAsync($"/api/npis/{id}/links", new AddLinkRequest("https://example.com/ok.pdf", "Correo del cliente", AttachmentContext.ClientApproval))).Content.ReadFromJsonAsync<AttachmentDto>();
        (await comercial.PostAsJsonAsync($"/api/npis/{id}/printcard-approvals", new ApprovePrintCardRequest(ApproverRole.Client, null, clientEvidence!.Id))).StatusCode.Should().Be(HttpStatusCode.OK);

        // 5) Cargar PrintCard (no termina el ciclo — decisión 3)
        var loadResponse = await disenador.PostAsync($"/api/npis/{id}/load-printcard", null);
        var afterLoad = await loadResponse.Content.ReadFromJsonAsync<NpiDetailDto>();
        afterLoad!.LifecycleStatus.Should().Be("Active");

        // 6) SAP + ET
        (await ingeniero.PostAsJsonAsync($"/api/npis/{id}/register-sap-pt", new RegisterSapPtRequest("PT-LC-001", "Nombre PT"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await comercial.PostAsJsonAsync($"/api/npis/{id}/register-sap-order", new RegisterSapOrderRequest("4500099999"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ingeniero.PostAsync($"/api/npis/{id}/register-et", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var finalResponse = await ingeniero.PostAsync($"/api/npis/{id}/load-et-sispro", null);

        finalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var final = await finalResponse.Content.ReadFromJsonAsync<NpiDetailDto>();
        final!.LifecycleStatus.Should().Be("Completed");

        // 7) Notificaciones generadas a lo largo del ciclo
        var notificationsResponse = await ingeniero.GetAsync("/api/notifications");
        notificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<Guid> GetUserIdAsync(HttpClient adminClient, string email)
    {
        var users = await adminClient.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/users");
        var match = users!.Single(u => u.GetProperty("email").GetString() == email);
        return match.GetProperty("id").GetGuid();
    }
}
