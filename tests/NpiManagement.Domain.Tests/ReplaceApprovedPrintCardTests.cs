using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Diseño reemplaza un PrintCard ya aprobado, hasta que se registra el PT (2026-11-11).</summary>
public class ReplaceApprovedPrintCardTests
{
    private readonly TestClock _clock = TestClock.MondayMorning();

    [Fact]
    public void A_full_replacement_goes_again_through_engineering_and_the_client()
    {
        var npi = CreateWithFullyApprovedPrintCard(_clock);

        var v2 = npi.ReplaceApprovedPrintCard(Designer, _clock, Calendar, "PC-0002", File(npi, _clock), "Se corrigió el código de barras", true, true);

        npi.PrintCardVersions.Single(v => v.VersionNumber == 1).IsRejected.Should().BeTrue();
        v2.VersionNumber.Should().Be(2);
        v2.Approvals.Should().NotContain(a => a.Role == ApproverRole.Engineering);
        v2.Approvals.Should().NotContain(a => a.Role == ApproverRole.Client);
        npi.PrintCardSentToClientAt.Should().BeNull();
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.AuditLog.Should().Contain(e => e.Action == "PrintCardReplacedByDesign" && e.Details!.Contains("Se corrigió el código de barras"));
    }

    [Fact]
    public void Without_new_approvals_it_inherits_them_and_goes_straight_to_loading()
    {
        var npi = CreateWithFullyApprovedPrintCard(_clock);

        var v2 = npi.ReplaceApprovedPrintCard(Designer, _clock, Calendar, "PC-0002", File(npi, _clock), "Ajuste interno de capas", false, false);

        v2.IsFullyApproved.Should().BeTrue();
        v2.Approvals.Single(a => a.Role == ApproverRole.Client).Note.Should().Contain("Heredada de la v1");
        npi.PrintCardSentToClientAt.Should().NotBeNull();
        npi.DesignTimer.IsRunning.Should().BeTrue();
        // Ya se puede cargar en sistema.
        npi.LoadPrintCardIntoSystem(Designer, _clock, Calendar);
        npi.PrintCardLoadedVersionId.Should().Be(v2.Id);
    }

    [Fact]
    public void Only_engineering_again_keeps_the_client_approval_and_then_design_loads_it()
    {
        var npi = CreateWithFullyApprovedPrintCard(_clock);

        npi.ReplaceApprovedPrintCard(Designer, _clock, Calendar, "PC-0002", File(npi, _clock), "Cambio de tolerancia", true, false);
        npi.ApprovePrintCard(Engineer, _clock, Calendar, ApproverRole.Engineering, null, null);

        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeTrue();
        npi.DesignTimer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void A_loaded_printcard_is_unloaded_and_the_limit_is_the_registered_pt()
    {
        var npi = CreateWithPrintCardLoaded(_clock);
        npi.ReplaceApprovedPrintCard(Designer, _clock, Calendar, "PC-0002", File(npi, _clock), "Corrección", false, false);
        npi.PrintCardLoadedAt.Should().BeNull();

        npi.LoadPrintCardIntoSystem(Designer, _clock, Calendar);
        npi.RegisterSapPt(Engineer, _clock, Calendar, "PT12543", null);
        FluentActions.Invoking(() => npi.ReplaceApprovedPrintCard(Designer, _clock, Calendar, "PC-0003", File(npi, _clock), "Otra", true, true))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-replace-after-pt");
    }

    [Fact]
    public void It_needs_a_reason_and_an_approved_version()
    {
        var npi = CreateWithFullyApprovedPrintCard(_clock);
        FluentActions.Invoking(() => npi.ReplaceApprovedPrintCard(Designer, _clock, Calendar, "PC-0002", File(npi, _clock), "  ", true, true))
            .Should().Throw<NpiValidationException>().Which.Code.Should().Be("printcard-replace-reason-required");

        var inReview = CreateReadyForPrintCard(_clock);
        inReview.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0001", File(inReview, _clock), null);
        FluentActions.Invoking(() => inReview.ReplaceApprovedPrintCard(Designer, _clock, Calendar, "PC-0002", File(inReview, _clock), "x", true, true))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-not-approved-yet");
    }
}
