using FluentAssertions;
using NpiManagement.Domain.Entities;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class NpiPrintCardTests
{
    [Theory]
    [InlineData(false, true, true, true, "design-not-assigned")]
    [InlineData(true, false, true, true, "engineering-feasibility-missing")]
    [InlineData(true, true, false, true, "design-feasibility-missing")]
    [InlineData(true, true, true, false, "artwork-not-validated")]
    public void CreatePrintCardVersion_requires_all_four_preconditions_decision_2(
        bool assignDesign, bool engFeasible, bool designFeasible, bool artworkValidated, string expectedCode)
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);
        npi.AssignEngineering(Engineer.UserId, EngineeringAdmin, clock);
        if (assignDesign) npi.AssignDesign(Designer.UserId, DesignAdmin, clock);
        if (engFeasible) npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);
        if (designFeasible && assignDesign) npi.ApproveDesignFeasibility(Designer, clock, null);
        if (artworkValidated && assignDesign) npi.ValidateArtwork(Designer, clock);

        var act = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void CreatePrintCardVersion_succeeds_when_all_four_preconditions_are_met()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);

        npi.PrintCardVersions.Should().ContainSingle();
        npi.CurrentPrintCardVersion!.VersionNumber.Should().Be(1);
        npi.CommercialTimer.IsRunning.Should().BeTrue();
        npi.DesignTimer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void CurrentPrintCardVersion_is_the_highest_version_even_if_the_collection_loads_out_of_order()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0002", null, null);

        // EF Core puede materializar la colección en cualquier orden (llaves GUID).
        var versiones = (List<PrintCardVersion>)typeof(Npi)
            .GetField("_printCardVersions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(npi)!;
        versiones.Reverse();

        npi.CurrentPrintCardVersion!.VersionNumber.Should().Be(2);
        npi.CurrentPrintCardVersion.Code.Should().Be("PC-0002");
    }

    [Fact]
    public void CreatePrintCardVersion_requires_a_non_empty_code()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        var act = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "  ", null, null);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("printcard-code-required");
    }

    [Fact]
    public void ApprovePrintCard_client_role_without_evidence_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);
        npi.SendPrintCardToClient(Commercial, clock);

        var act = () => npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, null);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("client-approval-evidence-required");
    }

    [Fact]
    public void ApprovePrintCard_same_role_twice_on_the_same_version_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);
        npi.ApprovePrintCard(Designer, clock, Calendar, ApproverRole.Design, null, null);

        var act = () => npi.ApprovePrintCard(Designer, clock, Calendar, ApproverRole.Design, null, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-already-approved-by-role");
    }

    [Fact]
    public void PrintCardVersion_is_fully_approved_only_after_the_three_roles_approve()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);

        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeFalse();
        npi.ApprovePrintCard(Designer, clock, Calendar, ApproverRole.Design, null, null);
        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeFalse();
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeFalse();
        npi.SendPrintCardToClient(Commercial, clock);
        npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, Guid.NewGuid());
        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeTrue();
    }

    [Fact]
    public void NewPrintCardVersion_does_not_inherit_approvals_from_the_previous_version_invariant_4()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);
        npi.ApprovePrintCard(Designer, clock, Calendar, ApproverRole.Design, null, null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, clock);

        // El cliente pide cambios en vez de aprobar: la v1 queda con 2 aprobaciones, nunca las 3.
        npi.RequestClientChanges(Commercial, clock, Calendar, "El cliente pide otro color");
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0002", null, null);

        npi.PrintCardVersions.Should().HaveCount(2);
        npi.PrintCardVersions.First().Approvals.Should().HaveCount(2, "las aprobaciones de una versión anterior quedan históricas, ligadas a su propia versión");
        npi.CurrentPrintCardVersion!.VersionNumber.Should().Be(2);
        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeFalse();
        npi.CurrentPrintCardVersion!.Approvals.Should().BeEmpty("la nueva versión no hereda las aprobaciones de la anterior");
    }

    [Fact]
    public void RejectByClient_requires_a_mandatory_comment_decision_7()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithFullyApprovedPrintCard(clock);

        var act = () => npi.RejectByClient(Commercial, clock, Calendar, "   ", null);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("client-rejection-comment-required");
    }

    [Fact]
    public void RejectByClient_evidence_is_optional_decision_7()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithFullyApprovedPrintCard(clock);

        npi.RejectByClient(Commercial, clock, Calendar, "El cliente cancela definitivamente", null);

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Rejected);
        npi.TerminalReason.Should().Be(TerminalReason.ClientRejection);
        npi.ClientRejection!.EvidenceAttachmentId.Should().BeNull();
    }

    [Fact]
    public void RejectByClient_closes_all_timers_as_a_terminal_rejection()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithFullyApprovedPrintCard(clock);

        npi.RejectByClient(Commercial, clock, Calendar, "El cliente cancela definitivamente", Guid.NewGuid());

        npi.GlobalTimer.IsClosed.Should().BeTrue();
        npi.EngineeringTimer.IsClosed.Should().BeTrue();
        npi.CommercialTimer.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void LoadPrintCardIntoSystem_requires_client_approval_first()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);
        npi.ApprovePrintCard(Designer, clock, Calendar, ApproverRole.Design, null, null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);

        var act = () => npi.LoadPrintCardIntoSystem(Designer, clock, Calendar);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("client-approval-missing");
    }

    [Fact]
    public void LoadPrintCardIntoSystem_only_closes_design_timer_and_is_not_terminal_decision_3()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithFullyApprovedPrintCard(clock);

        npi.LoadPrintCardIntoSystem(Designer, clock, Calendar);

        npi.DesignTimer.IsClosed.Should().BeTrue();
        npi.LifecycleStatus.Should().Be(LifecycleStatus.Active, "cargar el PrintCard no cierra el ciclo (decisión 3)");
        npi.GlobalTimer.IsClosed.Should().BeFalse();
        npi.EngineeringTimer.IsClosed.Should().BeFalse();
        npi.CommercialTimer.IsClosed.Should().BeFalse();
    }
}
