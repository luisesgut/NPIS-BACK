using FluentAssertions;
using NpiManagement.Application.Npis;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public class NpiStageMilestonesTests
{
    private static readonly NpiStageMilestones NothingDone = new(false, false, false, false, false, false, false, false);

    [Fact]
    public void A_new_npi_already_counts_the_commercial_stage()
    {
        NothingDone.CompletedStages.Should().Be(1);
    }

    [Fact]
    public void Every_milestone_reached_completes_all_eight_stages()
    {
        var allDone = new NpiStageMilestones(true, true, true, true, true, true, true, true);

        allDone.CompletedStages.Should().Be(NpiStageMilestones.StageCount);
    }

    [Fact]
    public void Both_feasibilities_count_as_separate_stages()
    {
        (NothingDone with { EngineeringFeasibilityApproved = true, DesignFeasibilityApproved = true })
            .CompletedStages.Should().Be(3);
    }

    [Fact]
    public void System_registration_needs_both_the_printcard_and_the_pt()
    {
        (NothingDone with { PrintCardLoaded = true }).CompletedStages.Should().Be(1);
        (NothingDone with { PtRegistered = true }).CompletedStages.Should().Be(1);
        (NothingDone with { PrintCardLoaded = true, PtRegistered = true }).CompletedStages.Should().Be(2);
    }
}
