using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>Fotos y archivos en el chat del NPI (2026-11-19).</summary>
public class ChatAttachmentsTests
{
    private readonly TestClock _clock = TestClock.Default();
    private readonly Actor _commercial = new(Guid.NewGuid(), "UsuarioComercial", "Vendedor");
    private readonly FakeNpiChatStore _chat = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeChatRealtime _realtime = new();

    private CurrentUser Commercial => new(_commercial.UserId, "UsuarioComercial", AreaType.Comercial);

    private AddCommentCommandHandler Handler() => new(_chat, new FakeUserDirectory(), new FakeUnitOfWork(), _clock, _notifications, _realtime);

    private Npi ActiveNpi()
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), _commercial, _clock, new NpiCaptureDto().ToDomain(), Priority.Media, Guid.NewGuid());
        npi.Submit(_commercial, _clock, new MexicoBusinessCalendar(), NpiNumber.FromSequenceValue(7));
        _chat.With(npi);
        return npi;
    }

    [Fact]
    public async Task A_message_can_be_only_a_photo_and_the_list_returns_it()
    {
        var npi = ActiveNpi();
        var photo = npi.RegisterAttachment(_commercial, _clock, "captura.png", "image/png", 1200, "k1", null, AttachmentContext.Comment);

        var dto = await Handler().HandleAsync(new AddCommentCommand(npi.Id, Commercial, "Vendedor", "", AttachmentIds: [photo.Id]), CancellationToken.None);

        dto.Text.Should().BeEmpty();
        dto.Attachments.Should().ContainSingle().Which.FileName.Should().Be("captura.png");
        _notifications.Grouped.Values.Should().AllSatisfy(t => t.Should().Contain("Mandó un archivo"));

        var page = await new GetNpiCommentsQueryHandler(_chat).HandleAsync(new GetNpiCommentsQuery(npi.Id, Commercial), CancellationToken.None);
        page.Items.Single().Attachments.Should().ContainSingle().Which.Id.Should().Be(photo.Id);
    }

    [Fact]
    public async Task A_file_that_is_not_from_this_npi_chat_is_rejected()
    {
        var npi = ActiveNpi();
        var printCard = npi.RegisterAttachment(_commercial, _clock, "pc.pdf", "application/pdf", 1200, "k2", null, AttachmentContext.PrintCard);

        var act = () => Handler().HandleAsync(new AddCommentCommand(npi.Id, Commercial, "Vendedor", "mira", AttachmentIds: [printCard.Id, Guid.NewGuid()]), CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>();
        _chat.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_text_nor_files_it_is_still_an_empty_message()
    {
        var npi = ActiveNpi();

        var act = () => Handler().HandleAsync(new AddCommentCommand(npi.Id, Commercial, "Vendedor", "   "), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }
}
