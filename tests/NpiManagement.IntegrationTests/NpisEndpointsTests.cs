using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using NpiManagement.Api.Contracts.Auth;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>CRUD de borradores NPI, paginación/filtros, y aislamiento por usuario/área — extremo a extremo contra la API real.</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class NpisEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
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

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, AuthWebApplicationFactory.SeedPassword));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static CreateNpiDraftRequest SampleDraft(string cliente = "Cliente E2E") =>
        new(new NpiCaptureDto { Cliente = cliente, NumeroCotizacion = "1001", DescripcionProducto = "Producto E2E" }, NpiManagement.Domain.Priority.Media, null);

    [SkippableFact]
    public async Task Creating_a_draft_and_reading_it_back_round_trips_the_capture_data()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = await AuthenticatedClientAsync("lvega@bioflex.mx");

        var createResponse = await client.PostAsJsonAsync("/api/npis", SampleDraft("ACME Corp"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var getResponse = await client.GetAsync($"/api/npis/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await getResponse.Content.ReadFromJsonAsync<NpiDetailDto>();
        detail!.Capture.Cliente.Should().Be("ACME Corp");
        detail.LifecycleStatus.Should().Be("Draft");
        detail.NpiNumber.Should().BeNull("un borrador no tiene folio hasta que se envía (Fase 5)");
    }

    [SkippableFact]
    public async Task Only_Comercial_roles_can_create_a_draft()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = await AuthenticatedClientAsync("zgonzalez@bioflex.mx"); // UsuarioIngenieria

        var response = await client.PostAsJsonAsync("/api/npis", SampleDraft());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task An_unrelated_commercial_user_gets_404_not_403_when_reading_someone_elses_draft()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var owner = await AuthenticatedClientAsync("lvega@bioflex.mx");
        var createResponse = await owner.PostAsJsonAsync("/api/npis", SampleDraft());
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var stranger = await AuthenticatedClientAsync("sgomez@bioflex.mx"); // otro UsuarioComercial, sin relación
        var response = await stranger.GetAsync($"/api/npis/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "no debe revelar que el NPI existe (authorization-matrix.md §4.6)");
    }

    [SkippableFact]
    public async Task AdminComercial_can_read_any_draft()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var owner = await AuthenticatedClientAsync("lvega@bioflex.mx");
        var createResponse = await owner.PostAsJsonAsync("/api/npis", SampleDraft());
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var admin = await AuthenticatedClientAsync("bbeas@bioflex.mx"); // AdminComercial
        var response = await admin.GetAsync($"/api/npis/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Updating_a_draft_persists_the_new_capture_data_and_returns_a_fresh_row_version()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = await AuthenticatedClientAsync("lvega@bioflex.mx");
        var createResponse = await client.PostAsJsonAsync("/api/npis", SampleDraft("Antes"));
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();
        var original = await (await client.GetAsync($"/api/npis/{id}")).Content.ReadFromJsonAsync<NpiDetailDto>();

        var updateResponse = await client.PatchAsJsonAsync($"/api/npis/{id}",
            new UpdateNpiDraftRequest(new NpiCaptureDto { Cliente = "Después", NumeroCotizacion = "1002" }, NpiManagement.Domain.Priority.Alta, original!.RowVersion));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<NpiDetailDto>();
        updated!.Capture.Cliente.Should().Be("Después");
        updated.Priority.Should().Be("Alta");
        updated.RowVersion.Should().NotEqual(original.RowVersion, "cada escritura exitosa debe avanzar el RowVersion");

        var detail = await (await client.GetAsync($"/api/npis/{id}")).Content.ReadFromJsonAsync<NpiDetailDto>();
        detail!.Capture.Cliente.Should().Be("Después");
    }

    [SkippableFact]
    public async Task Updating_a_draft_with_a_stale_row_version_returns_409()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = await AuthenticatedClientAsync("lvega@bioflex.mx");
        var createResponse = await client.PostAsJsonAsync("/api/npis", SampleDraft("Antes"));
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();
        var original = await (await client.GetAsync($"/api/npis/{id}")).Content.ReadFromJsonAsync<NpiDetailDto>();

        // Alguien más (o esta misma pestaña en otra acción) ya avanzó el RowVersion.
        (await client.PatchAsJsonAsync($"/api/npis/{id}",
            new UpdateNpiDraftRequest(new NpiCaptureDto { Cliente = "Primero", NumeroCotizacion = "1002" }, NpiManagement.Domain.Priority.Alta, original!.RowVersion)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Un segundo intento con el RowVersion original (ya obsoleto) debe rechazarse.
        var staleResponse = await client.PatchAsJsonAsync($"/api/npis/{id}",
            new UpdateNpiDraftRequest(new NpiCaptureDto { Cliente = "Segundo (obsoleto)", NumeroCotizacion = "1002" }, NpiManagement.Domain.Priority.Media, original.RowVersion));

        staleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SkippableFact]
    public async Task An_unrelated_user_cannot_update_someone_elses_draft()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var owner = await AuthenticatedClientAsync("lvega@bioflex.mx");
        var createResponse = await owner.PostAsJsonAsync("/api/npis", SampleDraft());
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var stranger = await AuthenticatedClientAsync("sgomez@bioflex.mx");
        var response = await stranger.PatchAsJsonAsync($"/api/npis/{id}", new UpdateNpiDraftRequest(new NpiCaptureDto(), NpiManagement.Domain.Priority.Media, []));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task List_only_returns_the_current_users_own_drafts_for_a_regular_commercial_user()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var mine = await AuthenticatedClientAsync("lvega@bioflex.mx");
        var marker = Guid.NewGuid().ToString("N")[..8];
        await mine.PostAsJsonAsync("/api/npis", SampleDraft($"Marca-{marker}"));

        var other = await AuthenticatedClientAsync("sgomez@bioflex.mx");
        await other.PostAsJsonAsync("/api/npis", SampleDraft($"Marca-{marker}"));

        var response = await mine.GetAsync($"/api/npis?search=Marca-{marker}&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<NpiListItemDto>>();

        page!.Items.Should().ContainSingle("el usuario sólo debe ver el propio, no el de 'other', aunque ambos coincidan en la búsqueda");
        page.Items.Single().CompletedStages.Should().Be(1, "un borrador sólo tiene completada la etapa comercial");
    }

    [SkippableFact]
    public async Task AdminTotal_sees_drafts_from_every_user_in_the_list()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var userA = await AuthenticatedClientAsync("lvega@bioflex.mx");
        await userA.PostAsJsonAsync("/api/npis", SampleDraft($"Total-{marker}"));
        var userB = await AuthenticatedClientAsync("sgomez@bioflex.mx");
        await userB.PostAsJsonAsync("/api/npis", SampleDraft($"Total-{marker}"));

        var admin = await AuthenticatedClientAsync("evelazquez@bioflex.mx"); // AdminTotal
        var response = await admin.GetAsync($"/api/npis?search=Total-{marker}&pageSize=50");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<NpiListItemDto>>();

        page!.Items.Should().HaveCount(2);
    }

    [SkippableFact]
    public async Task Users_endpoint_is_admin_only()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var regular = await AuthenticatedClientAsync("lvega@bioflex.mx");

        var response = await regular.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Quotations_lookup_returns_the_seeded_quotation()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = await AuthenticatedClientAsync("lvega@bioflex.mx");

        var response = await client.GetAsync("/api/quotations/1001");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Catalogs_endpoint_returns_the_seeded_catalogs()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = await AuthenticatedClientAsync("lvega@bioflex.mx");

        var response = await client.GetAsync("/api/catalogs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalogs = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        catalogs.Should().NotBeNullOrEmpty();
    }
}
