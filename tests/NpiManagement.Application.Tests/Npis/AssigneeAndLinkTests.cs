using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public sealed class AssigneeAndLinkTests
{
    private static readonly Guid CommercialId = Guid.NewGuid();
    private static readonly CurrentUser Commercial = new(CommercialId, "UsuarioComercial", AreaType.Comercial);

    private static async Task<(FakeNpiRepository repo, Guid npiId)> NewDraftAsync(FakeUserDirectory users)
    {
        var repo = new FakeNpiRepository();
        var npiId = await new CreateNpiDraftCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), users)
            .HandleAsync(new CreateNpiDraftCommand(CommercialId, "UsuarioComercial", "Comercial", new NpiCaptureDto(), Priority.Media, null), CancellationToken.None);
        return (repo, npiId);
    }

    [Fact]
    public async Task Preassigning_a_user_who_is_not_an_engineer_is_rejected()
    {
        var vendorId = Guid.NewGuid();
        var users = new FakeUserDirectory().With(vendorId, AreaType.Comercial, "UsuarioComercial");
        var handler = new CreateNpiDraftCommandHandler(new FakeNpiRepository(), new FakeUnitOfWork(), TestClock.Default(), users);

        var act = () => handler.HandleAsync(
            new CreateNpiDraftCommand(Guid.NewGuid(), "AdminComercial", "Admin Com", new NpiCaptureDto(), Priority.Media, vendorId),
            CancellationToken.None);

        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Code.Should().Be("assignee-not-valid");
    }

    [Fact]
    public async Task Assigning_a_disabled_engineer_is_rejected()
    {
        var engineerId = Guid.NewGuid();
        var users = new FakeUserDirectory().With(engineerId, AreaType.Ingenieria, "UsuarioIngenieria", UserStatus.Disabled);
        var (repo, npiId) = await NewDraftAsync(users);
        var handler = new AssignEngineeringCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new MexicoBusinessCalendar(), new FakeNotificationService(), users);

        var act = () => handler.HandleAsync(
            new AssignEngineeringCommand(npiId, new CurrentUser(Guid.NewGuid(), "AdminIngenieria", AreaType.Ingenieria), "Admin", engineerId),
            CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public async Task Commercial_can_add_a_link_and_it_is_listed_as_an_attachment()
    {
        var (repo, npiId) = await NewDraftAsync(new FakeUserDirectory());
        var handler = new RegisterLinkCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeNotificationService());

        var dto = await handler.HandleAsync(
            new RegisterLinkCommand(npiId, Commercial, "Comercial", "  https://we.tl/t-abc123  ", null, AttachmentContext.Artwork),
            CancellationToken.None);

        dto.Url.Should().Be("https://we.tl/t-abc123");
        dto.FileName.Should().Be("we.tl");
        dto.Context.Should().Be("Artwork");
        var npi = await repo.GetByIdAsync(npiId, CancellationToken.None);
        npi!.Attachments.Should().ContainSingle(a => a.IsLink);
    }

    [Theory]
    [InlineData("we.tl/t-abc123")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://servidor/archivo.ai")]
    public async Task Links_must_be_absolute_http_urls(string url)
    {
        var (repo, npiId) = await NewDraftAsync(new FakeUserDirectory());
        var handler = new RegisterLinkCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeNotificationService());

        var act = () => handler.HandleAsync(new RegisterLinkCommand(npiId, Commercial, "Comercial", url, null, AttachmentContext.Artwork), CancellationToken.None);

        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Code.Should().Be("link-invalid");
    }

    [Fact]
    public async Task Engineering_cannot_add_links()
    {
        var (repo, npiId) = await NewDraftAsync(new FakeUserDirectory());
        var handler = new RegisterLinkCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeNotificationService());
        var engineeringAdmin = new CurrentUser(Guid.NewGuid(), "AdminIngenieria", AreaType.Ingenieria);

        var act = () => handler.HandleAsync(new RegisterLinkCommand(npiId, engineeringAdmin, "Admin Ing", "https://example.com/a", null, AttachmentContext.Artwork), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
    private static async Task<(FakeNpiRepository repo, Guid npiId, Guid designerId)> NewActiveNpiWithDesignerAsync()
    {
        var designerId = Guid.NewGuid();
        var users = new FakeUserDirectory().With(designerId, AreaType.Diseno, "UsuarioDiseno");
        var (repo, npiId) = await NewDraftAsync(users);
        var uow = new FakeUnitOfWork();
        var clock = TestClock.Default();
        var calendar = new MexicoBusinessCalendar();
        var notifications = new FakeNotificationService();
        await new SubmitNpiCommandHandler(repo, uow, clock, calendar, new FakeNpiNumberGenerator(), notifications, users)
            .HandleAsync(new SubmitNpiCommand(npiId, Commercial, "Comercial"), CancellationToken.None);
        await new AssignDesignCommandHandler(repo, uow, clock, calendar, notifications, users)
            .HandleAsync(new AssignDesignCommand(npiId, new CurrentUser(Guid.NewGuid(), "AdminDiseno", AreaType.Diseno), "Admin Dis", designerId), CancellationToken.None);
        return (repo, npiId, designerId);
    }

    [Fact]
    public async Task Adding_artwork_to_an_active_npi_notifies_the_designer()
    {
        var (repo, npiId, designerId) = await NewActiveNpiWithDesignerAsync();
        var notifications = new FakeNotificationService();
        var handler = new RegisterLinkCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), notifications);

        await handler.HandleAsync(new RegisterLinkCommand(npiId, Commercial, "Comercial", "https://example.com/artes", "Artes v2", AttachmentContext.Artwork), CancellationToken.None);

        var sent = notifications.Sent.Should().ContainSingle().Subject;
        sent.RecipientUserId.Should().Be(designerId);
        sent.Message.Should().Contain("artes");
    }

    [Fact]
    public async Task Links_that_are_not_artwork_do_not_notify_the_designer()
    {
        var (repo, npiId, _) = await NewActiveNpiWithDesignerAsync();
        var notifications = new FakeNotificationService();
        var handler = new RegisterLinkCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), notifications);

        await handler.HandleAsync(new RegisterLinkCommand(npiId, Commercial, "Comercial", "https://example.com/evidencia", null, AttachmentContext.ClientApproval), CancellationToken.None);

        notifications.Sent.Should().BeEmpty();
    }
}
