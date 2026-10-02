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

[Collection(IntegrationTestCollection.Name)]
public sealed class NpiCommentsAndAttachmentsEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
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

    private async Task<Guid> CreateDraftAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/npis", new CreateNpiDraftRequest(new NpiCaptureDto { Cliente = "Cliente Adjuntos" }, Priority.Media, null));
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    [SkippableFact]
    public async Task Adding_and_listing_comments_round_trips_through_the_api()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = await ClientAsync("lvega@bioflex.mx");
        var id = await CreateDraftAsync(client);

        var addResponse = await client.PostAsJsonAsync($"/api/npis/{id}/comments", new AddCommentRequest("Primer comentario"));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await client.GetAsync($"/api/npis/{id}/comments");
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<CommentDto>>();
        page!.Items.Should().ContainSingle(c => c.Text == "Primer comentario");
    }

    [SkippableFact]
    public async Task Uploading_a_valid_pdf_and_downloading_it_returns_the_same_bytes()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = await ClientAsync("lvega@bioflex.mx");
        var id = await CreateDraftAsync(client);

        var pdfBytes = "%PDF-1.4 contenido de integración"u8.ToArray();
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "evidencia.pdf");
        form.Add(new StringContent(nameof(AttachmentContext.Comment)), "context");

        var uploadResponse = await client.PostAsync($"/api/npis/{id}/attachments", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<AttachmentDto>();

        var downloadResponse = await client.GetAsync($"/api/npis/{id}/attachments/{attachment!.Id}/download");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloaded = await downloadResponse.Content.ReadAsByteArrayAsync();
        downloaded.Should().Equal(pdfBytes);
    }

    [SkippableFact]
    public async Task Uploading_a_disallowed_content_type_returns_400()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var client = await ClientAsync("lvega@bioflex.mx");
        var id = await CreateDraftAsync(client);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x4D, 0x5A]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
        form.Add(fileContent, "file", "virus.exe");
        form.Add(new StringContent(nameof(AttachmentContext.Comment)), "context");

        var response = await client.PostAsync($"/api/npis/{id}/attachments", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task An_unrelated_user_cannot_download_someone_elses_attachment()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var owner = await ClientAsync("lvega@bioflex.mx");
        var id = await CreateDraftAsync(owner);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("%PDF-1.4"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "privado.pdf");
        form.Add(new StringContent(nameof(AttachmentContext.Comment)), "context");
        var uploadResponse = await owner.PostAsync($"/api/npis/{id}/attachments", form);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<AttachmentDto>();

        var stranger = await ClientAsync("sgomez@bioflex.mx");
        var response = await stranger.GetAsync($"/api/npis/{id}/attachments/{attachment!.Id}/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
