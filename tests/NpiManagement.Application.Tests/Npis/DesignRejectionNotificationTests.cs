using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>Rechazos de Diseño: a quién se avisa y quién puede ejecutarlos.</summary>
public sealed class DesignRejectionNotificationTests
{
    private readonly TestClock _clock = TestClock.Default();
    private readonly MexicoBusinessCalendar _calendar = new();
    private readonly Actor _commercial = new(Guid.NewGuid(), "UsuarioComercial", "Vendedor");
    private readonly Guid _engineerId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly FakeNpiRepository _repo = new();
    private readonly FakeNotificationService _notifications = new();

    private CurrentUser Designer => new(_designerId, "UsuarioDiseno", AreaType.Diseno);
    private CurrentUser CommercialUser => new(_commercial.UserId, "UsuarioComercial", AreaType.Comercial);

    private Npi ActiveNpi()
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), _commercial, _clock, new NpiCaptureDto().ToDomain(), Priority.Media, _engineerId);
        npi.Submit(_commercial, _clock, _calendar, NpiNumber.FromSequenceValue(9));
        npi.AssignDesign(_designerId, new Actor(Guid.NewGuid(), "AdminDiseno", "Admin"), _clock);
        _repo.Add(npi);
        return npi;
    }

    [Fact]
    public async Task Design_changes_email_the_commercial_owner()
    {
        var npi = ActiveNpi();

        await new RequestDesignChangesCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _notifications)
            .HandleAsync(new RequestDesignChangesCommand(npi.Id, Designer, "Diseñadora", "Falta el pantone", ["basePantone"]), CancellationToken.None);

        _notifications.Sent.Should().ContainSingle().Which.Should().Be(new SentNotification(
            _commercial.UserId, "Diseño solicitó cambios en el NPI NPI-0009 (1 campo por corregir): Falta el pantone", npi.Id, NotificationChannel.BellAndEmail));
    }

    [Fact]
    public async Task Resubmitting_only_design_changes_notifies_the_designer_not_the_engineer()
    {
        var npi = ActiveNpi();
        npi.RequestDesignChanges(new Actor(_designerId, "UsuarioDiseno", "Diseñadora"), _clock, _calendar, "Falta el pantone", []);

        await new SubmitNpiCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, new FakeNpiNumberGenerator(), _notifications, new FakeUserDirectory())
            .HandleAsync(new SubmitNpiCommand(npi.Id, CommercialUser, "Vendedor", "Agregué el pantone"), CancellationToken.None);

        _notifications.Sent.Select(n => n.RecipientUserId).Should().Equal(_designerId);
    }

    [Fact]
    public async Task Design_no_feasibility_emails_commercial_and_tells_engineering()
    {
        var npi = ActiveNpi();
        var designAdmin = new CurrentUser(Guid.NewGuid(), "AdminDiseno", AreaType.Diseno);

        await new RejectDesignNoFeasibilityCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _notifications, new FakeUserDirectory())
            .HandleAsync(new RejectDesignNoFeasibilityCommand(npi.Id, designAdmin, "Admin Diseño", "Degradado imposible en flexo"), CancellationToken.None);

        _notifications.Sent.Should().BeEquivalentTo(new[]
        {
            new SentNotification(_commercial.UserId, "El NPI NPI-0009 fue rechazado por Diseño (no factible): Degradado imposible en flexo", npi.Id, NotificationChannel.BellAndEmail),
            new SentNotification(_engineerId, "El NPI NPI-0009 fue rechazado por Diseño (no factible): Degradado imposible en flexo", npi.Id, NotificationChannel.Bell),
        });
    }

    [Fact]
    public async Task Only_the_assigned_designer_or_a_design_admin_can_reject_artwork()
    {
        var npi = ActiveNpi();
        var otherDesigner = new CurrentUser(Guid.NewGuid(), "UsuarioDiseno", AreaType.Diseno);

        var act = async () => await new RejectArtworkCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _notifications)
            .HandleAsync(new RejectArtworkCommand(npi.Id, otherDesigner, "Otro", "Baja resolución", null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Returning_corrected_artwork_emails_the_designer()
    {
        var npi = ActiveNpi();
        npi.RejectArtwork(new Actor(_designerId, "UsuarioDiseno", "Diseñadora"), _clock, _calendar, "Baja resolución", null);
        var later = new TestClock(_clock.UtcNow.AddMinutes(30));
        npi.RegisterLink(_commercial, later, "Artes v2", "https://drive.example.com/v2", AttachmentContext.Artwork);
        var observation = npi.ChangeRequests.Single(c => c.Origin == ChangeRequestOrigin.ArtworkRejected).Checklist.Single();
        await new SetArtworkObservationCommandHandler(_repo, new FakeUnitOfWork(), later, _calendar)
            .HandleAsync(new SetArtworkObservationCommand(npi.Id, CommercialUser, "Vendedor", observation.Id, true), CancellationToken.None);

        await new ResolveArtworkRejectionCommandHandler(_repo, new FakeUnitOfWork(), later, _calendar, _notifications)
            .HandleAsync(new ResolveArtworkRejectionCommand(npi.Id, CommercialUser, "Vendedor", "En alta resolución"), CancellationToken.None);

        _notifications.Sent.Should().ContainSingle().Which.Should().Be(new SentNotification(
            _designerId, "Comercial subió artes corregidas en el NPI NPI-0009. Comentario: En alta resolución", npi.Id, NotificationChannel.BellAndEmail));
    }
}
