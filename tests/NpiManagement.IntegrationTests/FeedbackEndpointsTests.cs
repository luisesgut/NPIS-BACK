using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NpiManagement.Api.Contracts.Auth;
using NpiManagement.Api.Contracts.Feedback;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class FeedbackEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string AdminTotalEmail = "evelazquez@bioflex.mx";
    private const string ComercialEmail = "lvega@bioflex.mx";
    private const string AdminComercialEmail = "bbeas@bioflex.mx";

    /// <summary>Encabezado PNG real seguido de relleno — suficiente para pasar la validación de firma.</summary>
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

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

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, AuthWebApplicationFactory.SeedPassword));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static MultipartFormDataContent ReportForm(string description, byte[]? screenshot = null, string screenshotContentType = "image/png")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(nameof(FeedbackKind.Bug)), "kind" },
            { new StringContent(description), "description" },
            { new StringContent("/panel/comercial"), "pageUrl" },
            { new StringContent("1280x720"), "screenSize" },
        };

        if (screenshot is not null)
        {
            var file = new ByteArrayContent(screenshot);
            file.Headers.ContentType = new MediaTypeHeaderValue(screenshotContentType);
            form.Add(file, "screenshot", "captura.png");
        }

        return form;
    }

    [SkippableFact]
    public async Task A_report_with_screenshot_reaches_admin_total_with_its_image()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var reporter = await ClientAsync(ComercialEmail);

        var submit = await reporter.PostAsync("/api/feedback", ReportForm("No me deja guardar el borrador", PngBytes));
        submit.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await submit.Content.ReadFromJsonAsync<SubmitFeedbackResponse>(Json))!;

        var admin = await ClientAsync(AdminTotalEmail);
        var reports = await admin.GetFromJsonAsync<List<FeedbackReportDto>>("/api/feedback", Json);
        var report = reports!.Single(r => r.Id == created.Id);
        report.HasScreenshot.Should().BeTrue();
        report.ReporterEmail.Should().Be(ComercialEmail);
        report.Status.Should().Be(FeedbackStatus.New);

        var image = await admin.GetAsync($"/api/feedback/{created.Id}/screenshot");
        image.StatusCode.Should().Be(HttpStatusCode.OK);
        (await image.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);

        var mine = await reporter.GetFromJsonAsync<List<MyFeedbackDto>>("/api/feedback/mine", Json);
        mine.Should().Contain(r => r.Id == created.Id);
    }

    [SkippableFact]
    public async Task Only_admin_total_can_read_the_inbox_and_screenshots()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var reporter = await ClientAsync(ComercialEmail);
        var submit = await reporter.PostAsync("/api/feedback", ReportForm("Reporte privado del admin total", PngBytes));
        var created = (await submit.Content.ReadFromJsonAsync<SubmitFeedbackResponse>(Json))!;

        var areaAdmin = await ClientAsync(AdminComercialEmail);
        (await areaAdmin.GetAsync("/api/feedback")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await areaAdmin.GetAsync($"/api/feedback/{created.Id}/screenshot")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reporter.GetAsync($"/api/feedback/{created.Id}/screenshot")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task A_screenshot_whose_content_is_not_an_image_is_rejected()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var reporter = await ClientAsync(ComercialEmail);

        var response = await reporter.PostAsync("/api/feedback", ReportForm("Captura disfrazada", "%PDF-1.4"u8.ToArray()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Resolving_a_report_notifies_whoever_sent_it()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var reporter = await ClientAsync(ComercialEmail);
        var submit = await reporter.PostAsync("/api/feedback", ReportForm("El filtro de prioridad no responde"));
        var created = (await submit.Content.ReadFromJsonAsync<SubmitFeedbackResponse>(Json))!;

        var admin = await ClientAsync(AdminTotalEmail);
        var update = await admin.PostAsJsonAsync($"/api/feedback/{created.Id}/status", new UpdateFeedbackStatusRequest(FeedbackStatus.Resolved, "Corregido en la versión de hoy"), Json);
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notifications = await reporter.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Json);
        notifications.Should().Contain(n => n.Message.Contains("Resuelto") && n.Message.Contains("Corregido en la versión de hoy"));
    }

    [SkippableFact]
    public async Task Reporter_and_support_chat_on_a_report_and_nobody_else_sees_it()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var reporter = await ClientAsync(ComercialEmail);
        var submit = await reporter.PostAsync("/api/feedback", ReportForm("No carga el PrintCard"));
        var created = (await submit.Content.ReadFromJsonAsync<SubmitFeedbackResponse>(Json))!;

        var admin = await ClientAsync(AdminTotalEmail);
        (await admin.PostAsJsonAsync($"/api/feedback/{created.Id}/messages", new FeedbackMessageRequest("¿En qué NPI te pasa?"), Json)).EnsureSuccessStatusCode();
        (await reporter.PostAsJsonAsync($"/api/feedback/{created.Id}/messages", new FeedbackMessageRequest("En el NPI-0038"), Json)).EnsureSuccessStatusCode();

        var thread = await reporter.GetFromJsonAsync<List<FeedbackMessageDto>>($"/api/feedback/{created.Id}/messages", Json);
        thread!.Select(m => (m.FromSupport, m.Text)).Should().Equal((true, "¿En qué NPI te pasa?"), (false, "En el NPI-0038"));
        (await reporter.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Json)).Should().Contain(n => n.Message.Contains("Soporte te escribió"));

        var other = await ClientAsync(AdminComercialEmail);
        (await other.GetAsync($"/api/feedback/{created.Id}/messages")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task Only_admin_total_downloads_the_reports_excel()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var reporter = await ClientAsync(ComercialEmail);
        await reporter.PostAsync("/api/feedback", ReportForm("Quiero exportar esto"));

        var admin = await ClientAsync(AdminTotalEmail);
        var response = await admin.GetAsync("/api/feedback/export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await response.Content.ReadAsByteArrayAsync()).Take(2).Should().Equal((byte)'P', (byte)'K');

        (await reporter.GetAsync("/api/feedback/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
