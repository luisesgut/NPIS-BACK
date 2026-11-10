using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Exceptions;
using Xunit;

namespace NpiManagement.Application.Tests;

/// <summary>Equipo por NPI (2026-11-10): el participante actúa como el responsable de su área.</summary>
public class NpiTeamTests
{
    private static readonly Guid Seller = Guid.NewGuid();
    private static readonly Guid Engineer = Guid.NewGuid();
    private static readonly Guid Designer = Guid.NewGuid();
    private static readonly Guid OtherSeller = Guid.NewGuid();
    private static readonly Guid OtherDesigner = Guid.NewGuid();

    private static Npi BuildNpi(TestClock clock)
    {
        var seller = new Actor(Seller, "UsuarioComercial", "Vendedor");
        var npi = Npi.CreateDraft(Guid.NewGuid(), seller, clock, NpiManagement.Domain.ValueObjects.NpiCaptureData.Empty, Priority.Media);
        npi.AssignEngineering(Engineer, new Actor(Guid.NewGuid(), "AdminIngenieria", "Admin"), clock);
        npi.AssignDesign(Designer, new Actor(Guid.NewGuid(), "AdminDiseno", "Admin"), clock);
        return npi;
    }

    private static CurrentUser User(Guid id, string role, AreaType area) => new(id, role, area);

    [Fact]
    public void A_team_member_acts_like_the_responsible_of_their_area_only()
    {
        var clock = TestClock.Default();
        var npi = BuildNpi(clock);
        var otherSeller = User(OtherSeller, "UsuarioComercial", AreaType.Comercial);

        NpiAuthorization.CanActAsCommercial(otherSeller, npi).Should().BeFalse();
        NpiAuthorization.CanAccess(otherSeller, NpiAuthorization.OwnershipOf(npi)).Should().BeFalse();

        npi.AddParticipant(OtherSeller, AreaType.Comercial, new Actor(Seller, "UsuarioComercial", "Vendedor"), clock);

        NpiAuthorization.CanActAsCommercial(otherSeller, npi).Should().BeTrue();
        NpiAuthorization.CanManageDraft(otherSeller, npi).Should().BeTrue();
        NpiAuthorization.CanAccess(otherSeller, NpiAuthorization.OwnershipOf(npi)).Should().BeTrue();
        NpiAuthorization.BuildAccessPredicate(otherSeller).Compile()(npi).Should().BeTrue();
        // Sólo lo de su área: no aprueba por Diseño ni por Ingeniería.
        NpiAuthorization.CanActAsDesign(otherSeller, npi).Should().BeFalse();
        NpiAuthorization.CanActAsEngineering(otherSeller, npi).Should().BeFalse();
        // No puede invitar a más gente: eso es del responsable o del admin.
        NpiAuthorization.CanManageParticipants(otherSeller, npi, AreaType.Comercial).Should().BeFalse();
    }

    [Fact]
    public void A_seat_in_another_area_gives_nothing_if_the_role_changed()
    {
        var clock = TestClock.Default();
        var npi = BuildNpi(clock);
        npi.AddParticipant(OtherDesigner, AreaType.Diseno, new Actor(Designer, "UsuarioDiseno", "Diseñador"), clock);

        var nowSeller = User(OtherDesigner, "UsuarioComercial", AreaType.Comercial);
        NpiAuthorization.CanAccess(nowSeller, NpiAuthorization.OwnershipOf(npi)).Should().BeFalse();
        NpiAuthorization.CanActAsDesign(nowSeller, npi).Should().BeFalse();
        NpiAuthorization.BuildAccessPredicate(nowSeller).Compile()(npi).Should().BeFalse();

        var designer = User(OtherDesigner, "UsuarioDiseno", AreaType.Diseno);
        NpiAuthorization.CanActAsDesign(designer, npi).Should().BeTrue();
        NpiAuthorization.BuildAccessPredicate(designer).Compile()(npi).Should().BeTrue();
    }

    [Fact]
    public void The_responsible_and_admins_manage_the_team_of_their_area()
    {
        var clock = TestClock.Default();
        var npi = BuildNpi(clock);

        NpiAuthorization.CanManageParticipants(User(Seller, "UsuarioComercial", AreaType.Comercial), npi, AreaType.Comercial).Should().BeTrue();
        NpiAuthorization.CanManageParticipants(User(Seller, "UsuarioComercial", AreaType.Comercial), npi, AreaType.Diseno).Should().BeFalse();
        NpiAuthorization.CanManageParticipants(User(Designer, "UsuarioDiseno", AreaType.Diseno), npi, AreaType.Diseno).Should().BeTrue();
        NpiAuthorization.CanManageParticipants(User(Guid.NewGuid(), "AdminDiseno", AreaType.Diseno), npi, AreaType.Diseno).Should().BeTrue();
        NpiAuthorization.CanManageParticipants(User(Guid.NewGuid(), "AdminTotal", AreaType.Total), npi, AreaType.Ingenieria).Should().BeTrue();
    }

    [Fact]
    public void The_team_rejects_duplicates_and_the_responsible_and_leaves_when_promoted()
    {
        var clock = TestClock.Default();
        var npi = BuildNpi(clock);
        var admin = new Actor(Guid.NewGuid(), "AdminTotal", "Admin");

        FluentActions.Invoking(() => npi.AddParticipant(Designer, AreaType.Diseno, admin, clock))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("participant-is-responsible");

        npi.AddParticipant(OtherDesigner, AreaType.Diseno, admin, clock);
        FluentActions.Invoking(() => npi.AddParticipant(OtherDesigner, AreaType.Diseno, admin, clock))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("participant-already-added");
        npi.AuditLog.Should().Contain(e => e.Action == "ParticipantAdded");

        // Si pasa a ser el responsable, deja de estar repetido como participante.
        npi.AssignDesign(OtherDesigner, admin, clock);
        npi.Participants.Should().BeEmpty();

        npi.AddParticipant(Designer, AreaType.Diseno, admin, clock);
        npi.RemoveParticipant(Designer, AreaType.Diseno, admin, clock);
        npi.Participants.Should().BeEmpty();
        npi.AuditLog.Should().Contain(e => e.Action == "ParticipantRemoved");
    }
}
