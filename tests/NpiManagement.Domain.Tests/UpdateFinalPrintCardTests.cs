using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Diseño actualiza el PrintCard final ya cargado en sistema, aun con PT (2026-11-12).</summary>
public class UpdateFinalPrintCardTests
{
    private readonly TestClock _clock = TestClock.MondayMorning();

    [Fact]
    public void With_the_pt_registered_the_new_version_becomes_the_final_one_with_its_approvals()
    {
        var npi = CreateWithPrintCardLoaded(_clock);
        npi.RegisterSapPt(Engineer, _clock, Calendar, "PT12547", null);
        var loadedAt = npi.PrintCardLoadedAt;

        var v2 = npi.UpdateFinalPrintCard(Designer, _clock, "PC-0001-B", File(npi, _clock), "Se ajustó una cota; ya está en Advance");

        npi.FinalPrintCardVersion.Should().Be(v2);
        npi.PrintCardLoadedAt.Should().Be(loadedAt);
        v2.IsFullyApproved.Should().BeTrue();
        v2.Approvals.Single(a => a.Role == ApproverRole.Client).Note.Should().Contain("Heredada de la v1");
        npi.PrintCardVersions.Single(v => v.VersionNumber == 1).IsRejected.Should().BeFalse();
        npi.AuditLog.Should().Contain(e => e.Action == "PrintCardFinalUpdated" && e.Details!.Contains("ajustó una cota"));
    }

    [Fact]
    public void It_needs_a_loaded_printcard_and_a_reason()
    {
        var notLoaded = CreateWithFullyApprovedPrintCard(_clock);
        FluentActions.Invoking(() => notLoaded.UpdateFinalPrintCard(Designer, _clock, "PC", File(notLoaded, _clock), "x"))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-not-loaded");

        var npi = CreateWithPrintCardLoaded(_clock);
        FluentActions.Invoking(() => npi.UpdateFinalPrintCard(Designer, _clock, "PC", File(npi, _clock), " "))
            .Should().Throw<NpiValidationException>().Which.Code.Should().Be("printcard-update-reason-required");
    }
}
