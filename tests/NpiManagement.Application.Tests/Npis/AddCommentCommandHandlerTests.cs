using FluentAssertions;
using NpiManagement.Application.Abstractions;
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

/// <summary>Chat del NPI (antes comentarios): permisos, menciones, avisos agrupados y tiempo real.</summary>
public class AddCommentCommandHandlerTests
{
    private readonly TestClock _clock = TestClock.Default();
    private readonly Actor _commercial = new(Guid.NewGuid(), "UsuarioComercial", "Vendedor");
    private readonly Guid _engineerId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly FakeNpiChatStore _chat = new();
    private readonly FakeUserDirectory _users = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeChatRealtime _realtime = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CurrentUser Designer => new(_designerId, "UsuarioDiseno", AreaType.Diseno);
    private CurrentUser Commercial => new(_commercial.UserId, "UsuarioComercial", AreaType.Comercial);

    private AddCommentCommandHandler Handler() => new(_chat, _users, _unitOfWork, _clock, _notifications, _realtime);

    /// <summary>NPI-0007 enviado, con ingeniero y diseñadora asignados.</summary>
    private Npi ActiveNpi()
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), _commercial, _clock, new NpiCaptureDto().ToDomain(), Priority.Media, _engineerId);
        npi.Submit(_commercial, _clock, new MexicoBusinessCalendar(), NpiNumber.FromSequenceValue(7));
        npi.AssignDesign(_designerId, new Actor(Guid.NewGuid(), "AdminDiseno", "Admin"), _clock);
        _chat.With(npi);
        _users.Users.Add(new UserSummaryDto(_commercial.UserId, "Vendedor", "v@bioflex.mx", "UsuarioComercial", AreaType.Comercial, UserStatus.Active));
        _users.Users.Add(new UserSummaryDto(_engineerId, "Ingeniero", "i@bioflex.mx", "UsuarioIngenieria", AreaType.Ingenieria, UserStatus.Active));
        _users.Users.Add(new UserSummaryDto(_designerId, "Diseñadora", "d@bioflex.mx", "UsuarioDiseno", AreaType.Diseno, UserStatus.Active));
        return npi;
    }

    [Fact]
    public async Task Posting_saves_the_message_marks_it_read_for_the_author_and_pushes_it_in_real_time()
    {
        var npi = ActiveNpi();

        var dto = await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "  ¿El logo va en negro o en verde?  "), CancellationToken.None);

        dto.Text.Should().Be("¿El logo va en negro o en verde?");
        dto.NpiId.Should().Be(npi.Id);
        _chat.Messages.Should().ContainSingle();
        _chat.Reads[(npi.Id, _designerId)].Should().Be(dto.CreatedAt);
        _unitOfWork.SaveChangesCallCount.Should().Be(1);
        _realtime.Posted.Should().ContainSingle().Which.Notified.Should().BeEquivalentTo([_commercial.UserId, _engineerId]);
    }

    [Fact]
    public async Task Without_mentions_participants_get_one_grouped_bell_and_no_email()
    {
        var npi = ActiveNpi();
        var key = NotificationText.ChatGroupKey(npi.Id);

        await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "Primer mensaje"), CancellationToken.None);
        await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "Segundo mensaje"), CancellationToken.None);

        _notifications.Sent.Should().BeEmpty("sin menciones nadie recibe correo ni un aviso por mensaje");
        _notifications.Grouped.Keys.Should().BeEquivalentTo([(_commercial.UserId, key), (_engineerId, key)]);
        _notifications.Grouped[(_commercial.UserId, key)].Should().Be("Nuevos mensajes en el chat del NPI NPI-0007 — Diseñadora: Segundo mensaje");
    }

    [Fact]
    public async Task A_mention_sends_bell_and_email_only_to_the_mentioned_person()
    {
        var npi = ActiveNpi();

        await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "@Ingeniero ¿confirmas el calibre?", [_engineerId]), CancellationToken.None);

        _notifications.Sent.Should().ContainSingle().Which.Should().Be(new SentNotification(
            _engineerId, "Diseñadora te mencionó en el chat del NPI NPI-0007: @Ingeniero ¿confirmas el calibre?", npi.Id, NotificationChannel.BellAndEmail));
        _notifications.Grouped.Keys.Should().ContainSingle().Which.RecipientUserId.Should().Be(_commercial.UserId, "a quien mencionan no se le duplica el aviso");
    }

    [Fact]
    public async Task Cannot_mention_someone_who_cannot_see_the_npi()
    {
        var npi = ActiveNpi();
        var outsider = Guid.NewGuid();
        _users.Users.Add(new UserSummaryDto(outsider, "Otro ingeniero", "o@bioflex.mx", "UsuarioIngenieria", AreaType.Ingenieria, UserStatus.Active));

        var act = async () => await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "@Otro mira esto", [outsider]), CancellationToken.None);

        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Code.Should().Be("chat-invalid-mention");
        _chat.Messages.Should().BeEmpty();
        _realtime.Posted.Should().BeEmpty();
    }

    [Fact]
    public async Task Cannot_mention_a_disabled_user_even_if_assigned()
    {
        var npi = ActiveNpi();
        _users.Users.RemoveAll(u => u.Id == _engineerId);
        _users.Users.Add(new UserSummaryDto(_engineerId, "Ingeniero", "i@bioflex.mx", "UsuarioIngenieria", AreaType.Ingenieria, UserStatus.Disabled));

        var act = async () => await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "@Ingeniero", [_engineerId]), CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public async Task Writing_is_still_allowed_after_the_npi_becomes_terminal()
    {
        var npi = ActiveNpi();
        npi.RejectNoFeasibility(new Actor(_engineerId, "UsuarioIngenieria", "Ingeniero"), _clock, new MexicoBusinessCalendar(), null);

        var act = async () => await Handler().HandleAsync(new AddCommentCommand(npi.Id, Commercial, "Vendedor", "Nota histórica tras el cierre"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task An_unrelated_user_cannot_write()
    {
        var npi = ActiveNpi();
        var stranger = new CurrentUser(Guid.NewGuid(), "UsuarioComercial", AreaType.Comercial);

        var act = async () => await Handler().HandleAsync(new AddCommentCommand(npi.Id, stranger, "Extraño", "Intento no autorizado"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task An_admin_who_is_not_a_participant_can_read_but_not_write()
    {
        var npi = ActiveNpi();
        var admin = new CurrentUser(Guid.NewGuid(), "AdminTotal", AreaType.Total);
        await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "Hola"), CancellationToken.None);

        var write = async () => await Handler().HandleAsync(new AddCommentCommand(npi.Id, admin, "Admin", "Opino"), CancellationToken.None);

        (await write.Should().ThrowAsync<RequestValidationException>()).Which.Code.Should().Be("chat-not-participant");
        var page = await new GetNpiCommentsQueryHandler(_chat).HandleAsync(new GetNpiCommentsQuery(npi.Id, admin), CancellationToken.None);
        page.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task An_admin_who_is_assigned_can_write()
    {
        var npi = ActiveNpi();
        var adminEngineer = new CurrentUser(_engineerId, "AdminIngenieria", AreaType.Ingenieria);

        var act = async () => await Handler().HandleAsync(new AddCommentCommand(npi.Id, adminEngineer, "Ingeniero", "Yo lo reviso"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Cannot_mention_an_admin_who_is_not_a_participant()
    {
        var npi = ActiveNpi();
        var admin = Guid.NewGuid();
        _users.Users.Add(new UserSummaryDto(admin, "Admin Total", "t@bioflex.mx", "AdminTotal", AreaType.Total, UserStatus.Active));

        var act = async () => await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "@Admin mira", [admin]), CancellationToken.None);

        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Code.Should().Be("chat-invalid-mention");
    }

    [Fact]
    public async Task Opening_the_chat_marks_it_read_and_clears_the_grouped_bell()
    {
        var npi = ActiveNpi();
        await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "Hola"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));

        await new MarkChatReadCommandHandler(_chat, _unitOfWork, _clock, _notifications)
            .HandleAsync(new MarkChatReadCommand(npi.Id, Commercial), CancellationToken.None);

        _chat.Reads[(npi.Id, _commercial.UserId)].Should().Be(_clock.UtcNow);
        _notifications.Grouped.Keys.Should().NotContain((_commercial.UserId, NotificationText.ChatGroupKey(npi.Id)));
    }

    [Fact]
    public async Task Messages_are_listed_newest_first()
    {
        var npi = ActiveNpi();
        await Handler().HandleAsync(new AddCommentCommand(npi.Id, Designer, "Diseñadora", "Primero"), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await Handler().HandleAsync(new AddCommentCommand(npi.Id, Commercial, "Vendedor", "Segundo"), CancellationToken.None);

        var page = await new GetNpiCommentsQueryHandler(_chat).HandleAsync(new GetNpiCommentsQuery(npi.Id, Commercial), CancellationToken.None);

        page.Items.Select(m => m.Text).Should().Equal("Segundo", "Primero");
        page.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Mention_candidates_are_only_the_active_participants()
    {
        var npi = ActiveNpi();
        var admin = Guid.NewGuid();
        _users.Users.Add(new UserSummaryDto(admin, "Admin Ingeniería", "a@bioflex.mx", "AdminIngenieria", AreaType.Ingenieria, UserStatus.Active));
        _users.Users.Add(new UserSummaryDto(Guid.NewGuid(), "Otro vendedor", "x@bioflex.mx", "UsuarioComercial", AreaType.Comercial, UserStatus.Active));
        _users.Users.Add(new UserSummaryDto(Guid.NewGuid(), "Admin inactivo", "z@bioflex.mx", "AdminTotal", AreaType.Total, UserStatus.Disabled));

        var participants = await new GetChatParticipantsQueryHandler(_chat, _users).HandleAsync(new GetChatParticipantsQuery(npi.Id, Designer), CancellationToken.None);

        participants.Select(p => p.FullName).Should().Equal("Diseñadora", "Ingeniero", "Vendedor");
        participants.Should().NotContain(p => p.UserId == admin, "el admin puede leer pero no participa");
    }
}
