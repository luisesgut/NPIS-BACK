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
/// Fase 8: dos PATCH concurrentes sobre el mismo borrador deben producir exactamente un 200 y un
/// 409 (ApiExceptionHandler traduce DbUpdateConcurrencyException) — nunca dos 200 silenciosos que
/// pisen el cambio del otro. El cliente que recibe 409 debe volver a consultar el detalle, no
/// reintentar ciegamente (ver docs/frontend-integration.md y ApiExceptionHandler.cs).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ConcurrencyConflictEndpointTests(SqlServerFixture fixture) : IAsyncLifetime
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

    private async Task<HttpClient> ClientAsync()
    {
        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("lvega@bioflex.mx", AuthWebApplicationFactory.SeedPassword));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    [SkippableFact]
    public async Task Two_concurrent_updates_to_the_same_draft_yield_one_success_and_one_409()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var owner = await ClientAsync();

        var createResponse = await owner.PostAsJsonAsync("/api/npis",
            new CreateNpiDraftRequest(new NpiCaptureDto { Cliente = "Cliente Concurrencia", NumeroCotizacion = "1001" }, Priority.Media, null));
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();
        // Ambos "usuarios" parten de la misma versión — igual que si hubieran abierto el mismo
        // NPI en dos pestañas antes de que cualquiera de los dos guardara.
        var seen = await (await owner.GetAsync($"/api/npis/{id}")).Content.ReadFromJsonAsync<NpiDetailDto>();

        var clientA = await ClientAsync();
        var clientB = await ClientAsync();
        var requestA = clientA.PatchAsJsonAsync($"/api/npis/{id}", new UpdateNpiDraftRequest(new NpiCaptureDto { Cliente = "Actualizado por A", NumeroCotizacion = "1001" }, Priority.Alta, seen!.RowVersion));
        var requestB = clientB.PatchAsJsonAsync($"/api/npis/{id}", new UpdateNpiDraftRequest(new NpiCaptureDto { Cliente = "Actualizado por B", NumeroCotizacion = "1001" }, Priority.Baja, seen.RowVersion));

        var responses = await Task.WhenAll(requestA, requestB);
        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToList();

        statusCodes.Should().Contain(HttpStatusCode.OK);
        statusCodes.Should().Contain(HttpStatusCode.Conflict, "una de las dos escrituras concurrentes debe perder la carrera de RowVersion");

        var finalDetail = await (await owner.GetAsync($"/api/npis/{id}")).Content.ReadFromJsonAsync<NpiDetailDto>();
        finalDetail!.Capture.Cliente.Should().MatchRegex("Actualizado por (A|B)", "el registro final debe reflejar exactamente la escritura ganadora, no una mezcla");
    }
}
