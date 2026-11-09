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
    public void Every_milestone_reached_completes_all_ten_stages()
    {
        var allDone = new NpiStageMilestones(true, true, true, true, true, true, true, true, SisproQuoteLinked: true, SisproQuoteAccepted: true, HasPrintCard: true);

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

    [Fact]
    public void Linking_and_accepting_the_quote_are_two_stages()
    {
        (NothingDone with { SisproQuoteLinked = true }).CompletedStages.Should().Be(2);
        (NothingDone with { SisproQuoteLinked = true, SisproQuoteAccepted = true }).CompletedStages.Should().Be(3);
    }

    [Fact]
    public void A_npi_that_already_had_a_printcard_before_quotes_existed_does_not_get_stuck_on_them()
    {
        (NothingDone with { HasPrintCard = true }).CompletedStages.Should().Be(3);
    }

    [Fact]
    public void With_the_quote_stage_off_the_route_has_eight_stages_and_quotes_do_not_count()
    {
        using var _ = Domain.SisproQuoteGate.Override(false);

        NpiStageMilestones.StageCount.Should().Be(8);
        (NothingDone with { EngineeringFeasibilityApproved = true, SisproQuoteLinked = true, SisproQuoteAccepted = true, HasPrintCard = true })
            .CompletedStages.Should().Be(2);
        new NpiStageMilestones(true, true, true, true, true, true, true, true, SisproQuoteLinked: true, SisproQuoteAccepted: true, HasPrintCard: true)
            .CompletedStages.Should().Be(8);
    }
}
