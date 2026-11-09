using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Exceptions;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>Cierres de usuarios normales: quedan como propuesta hasta el visto bueno del admin de su área.</summary>
public sealed class ClosureProposalTests
{
    private readonly TestClock _clock = TestClock.Default();
    private readonly MexicoBusinessCalendar _calendar = new();
    private readonly Actor _commercial = new(Guid.NewGuid(), "UsuarioComercial", "Vendedor");
    private readonly Guid _engineerId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly Guid _engineeringAdminId = Guid.NewGuid();
    private readonly Guid _designAdminId = Guid.NewGuid();
    private readonly Guid _adminTotalId = Guid.NewGuid();
    private readonly FakeNpiRepository _repo = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeUserDirectory _users = new();

    private CurrentUser Engineer => new(_engineerId, "UsuarioIngenieria", AreaType.Ingenieria);
    private CurrentUser EngineeringAdmin => new(_engineeringAdminId, "AdminIngenieria", AreaType.Ingenieria);
    private CurrentUser DesignAdmin => new(_designAdminId, "AdminDiseno", AreaType.Diseno);
    private CurrentUser CommercialUser => new(_commercial.UserId, "UsuarioComercial", AreaType.Comercial);

    public ClosureProposalTests()
    {
        _users.With(_engineeringAdminId, AreaType.Ingenieria, "AdminIngenieria")
            .With(_designAdminId, AreaType.Diseno, "AdminDiseno")
            .With(_adminTotalId, AreaType.Total, "AdminTotal")
            .With(Guid.NewGuid(), AreaType.Ingenieria, "AdminIngenieria", UserStatus.Disabled);
    }

    private Npi ActiveNpi()
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), _commercial, _clock, new NpiCaptureDto().ToDomain(), Priority.Media, _engineerId);
        npi.Submit(_commercial, _clock, _calendar, NpiNumber.FromSequenceValue(7));
        npi.AssignDesign(_designerId, new Actor(Guid.NewGuid(), "AdminDiseno", "Admin"), _clock);
        _repo.Add(npi);
        return npi;
    }

    private Task<NpiDetailDto> ProposeNoFeasibility(Npi npi, string? note = "El calibre no se puede laminar") =>
        new RejectNoFeasibilityCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _notifications, _users)
            .HandleAsync(new RejectNoFeasibilityCommand(npi.Id, Engineer, "Ingeniero", note), CancellationToken.None);

    [Fact]
    public async Task A_normal_engineer_only_proposes_and_the_npi_stays_active()
    {
        var npi = ActiveNpi();

        var result = await ProposeNoFeasibility(npi);

        result.LifecycleStatus.Should().Be("Active");
        result.ClosureProposals.Should().ContainSingle().Which.Should().Match<ClosureProposalDto>(p =>
            p.Status == "Pending" && p.Reason == "NoFeasibility" && p.Area == "Ingenieria" && p.ProposedByName == "Ingeniero");
    }

    [Fact]
    public async Task The_proposal_alerts_only_active_engineering_admins_and_admin_total_not_commercial()
    {
        var npi = ActiveNpi();

        await ProposeNoFeasibility(npi);

        _notifications.Sent.Select(n => n.RecipientUserId).Should().BeEquivalentTo([_engineeringAdminId, _adminTotalId]);
        _notifications.Sent.Should().OnlyContain(n => n.Channel == NotificationChannel.BellAndEmail && n.Message.Contains("espera tu visto bueno"));
    }

    [Fact]
    public async Task A_normal_user_must_explain_the_reason()
    {
        var npi = ActiveNpi();

        var act = () => ProposeNoFeasibility(npi, note: " ");

        await act.Should().ThrowAsync<NpiValidationException>();
    }

    [Fact]
    public async Task Confirming_closes_the_npi_and_only_then_tells_commercial()
    {
        var npi = ActiveNpi();
        await ProposeNoFeasibility(npi);
        _notifications.Sent.Clear();

        var result = await new ConfirmClosureProposalCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _notifications)
            .HandleAsync(new ConfirmClosureProposalCommand(npi.Id, EngineeringAdmin, "Jefa de Ingeniería", "De acuerdo"), CancellationToken.None);

        result.LifecycleStatus.Should().Be("Rejected");
        result.TerminalReason.Should().Be("NoFeasibility");
        result.ClosureProposals.Single().Should().Match<ClosureProposalDto>(p => p.Status == "Confirmed" && p.DecidedByName == "Jefa de Ingeniería");
        _notifications.Sent.Should().Contain(n => n.RecipientUserId == _commercial.UserId && n.Channel == NotificationChannel.BellAndEmail);
        _notifications.Sent.Should().Contain(n => n.RecipientUserId == _engineerId);
    }

    [Fact]
    public async Task Declining_keeps_the_npi_going_and_tells_the_engineer_why()
    {
        var npi = ActiveNpi();
        await ProposeNoFeasibility(npi);
        _notifications.Sent.Clear();

        var result = await new DeclineClosureProposalCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _notifications)
            .HandleAsync(new DeclineClosureProposalCommand(npi.Id, EngineeringAdmin, "Jefa de Ingeniería", "Sí se puede con otro calibre"), CancellationToken.None);

        result.LifecycleStatus.Should().Be("Active");
        result.ClosureProposals.Single().Status.Should().Be("Declined");
        _notifications.Sent.Should().ContainSingle().Which.Should().Match<SentNotification>(n =>
            n.RecipientUserId == _engineerId && n.Message.Contains("Sí se puede con otro calibre"));

        // Ya puede seguir: aprobar la factibilidad.
        npi.ApproveEngineeringFeasibility(new Actor(_engineerId, "UsuarioIngenieria", "Ingeniero"), _clock, _calendar, null);
        npi.EngineeringFeasibility.Should().NotBeNull();
    }

    [Fact]
    public async Task While_under_review_the_area_cannot_move_forward()
    {
        var npi = ActiveNpi();
        await ProposeNoFeasibility(npi);

        var act = () => npi.ApproveEngineeringFeasibility(new Actor(_engineerId, "UsuarioIngenieria", "Ingeniero"), _clock, _calendar, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("closure-proposal-pending");
    }

    [Fact]
    public async Task Another_area_admin_cannot_decide_it()
    {
        var npi = ActiveNpi();
        await ProposeNoFeasibility(npi);

        var act = () => new ConfirmClosureProposalCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _notifications)
            .HandleAsync(new ConfirmClosureProposalCommand(npi.Id, DesignAdmin, "Admin Diseño", null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Only_the_proposer_can_withdraw_it()
    {
        var npi = ActiveNpi();
        await ProposeNoFeasibility(npi);
        var handler = new WithdrawClosureProposalCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar);

        var byOther = () => handler.HandleAsync(new WithdrawClosureProposalCommand(npi.Id, EngineeringAdmin, "Admin"), CancellationToken.None);
        await byOther.Should().ThrowAsync<NotFoundException>();

        var result = await handler.HandleAsync(new WithdrawClosureProposalCommand(npi.Id, Engineer, "Ingeniero"), CancellationToken.None);
        result.ClosureProposals.Single().Status.Should().Be("Withdrawn");
        result.LifecycleStatus.Should().Be("Active");
    }

    [Fact]
    public async Task An_engineering_admin_still_closes_directly()
    {
        var npi = ActiveNpi();

        var result = await new RejectNoFeasibilityCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _notifications, _users)
            .HandleAsync(new RejectNoFeasibilityCommand(npi.Id, EngineeringAdmin, "Admin", "No se puede"), CancellationToken.None);

        result.LifecycleStatus.Should().Be("Rejected");
        result.ClosureProposals.Should().BeEmpty();
    }

    [Fact]
    public async Task A_normal_commercial_user_only_proposes_the_total_supplies_rejection()
    {
        var npi = ActiveNpi();
        npi.RejectSuppliesTemporary(new Actor(_engineerId, "UsuarioIngenieria", "Ingeniero"), _clock, _calendar, "Falta resina");
        _users.With(Guid.NewGuid(), AreaType.Comercial, "AdminComercial");

        var result = await new RejectSuppliesTotalCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _notifications, _users)
            .HandleAsync(new RejectSuppliesTotalCommand(npi.Id, CommercialUser, "Vendedor", "El proveedor no tendrá resina este año"), CancellationToken.None);

        result.LifecycleStatus.Should().Be("Active");
        result.ClosureProposals.Single().Should().Match<ClosureProposalDto>(p => p.Area == "Comercial" && p.Reason == "SuppliesRejectionTotal");
    }

    [Fact]
    public async Task Cancelling_leaves_a_pending_proposal_without_effect()
    {
        var npi = ActiveNpi();
        await ProposeNoFeasibility(npi);

        npi.Cancel(new Actor(Guid.NewGuid(), "AdminComercial", "Admin Comercial"), _clock, _calendar, "El cliente desistió");

        npi.ClosureProposals.Single().Status.Should().Be(ClosureProposalStatus.Superseded);
        npi.PendingClosureProposal.Should().BeNull();
    }
}
