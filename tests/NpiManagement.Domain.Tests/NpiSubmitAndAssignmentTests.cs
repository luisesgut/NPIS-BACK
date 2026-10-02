using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using NpiManagement.Domain.ValueObjects;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class NpiSubmitAndAssignmentTests
{
    [Fact]
    public void CreateDraft_starts_as_Draft_without_number_or_running_timers()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateDraft(clock);

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Draft);
        npi.NpiNumber.Should().BeNull();
        npi.GlobalTimer.HasStarted.Should().BeFalse();
        npi.EngineeringTimer.HasStarted.Should().BeFalse();
        npi.DesignTimer.HasStarted.Should().BeFalse();
        npi.AssignedCommercialUserId.Should().Be(Commercial.UserId);
    }

    [Fact]
    public void Submit_without_a_number_throws_on_first_submission()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateDraft(clock);

        var act = () => npi.Submit(Commercial, clock, Calendar);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("npi-number-required");
    }

    [Fact]
    public void Submit_first_time_assigns_number_activates_and_starts_global_and_engineering_timers()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateDraft(clock);

        npi.Submit(Commercial, clock, Calendar, NpiNumber.FromSequenceValue(4));

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Active);
        npi.NpiNumber!.Value.Should().Be("NPI-0004");
        npi.GlobalTimer.IsRunning.Should().BeTrue();
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.DesignTimer.HasStarted.Should().BeFalse("Diseño nunca arranca automáticamente al enviar");
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.Requested);
    }

    [Fact]
    public void Submit_twice_on_an_active_npi_without_changes_requested_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);

        var act = () => npi.Submit(Commercial, clock, Calendar);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("npi-not-submittable");
    }

    [Fact]
    public void Resubmit_after_changes_requested_resumes_engineering_and_pauses_commercial()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RequestEngineeringChanges(Engineer, clock, Calendar, "Faltan datos", []);
        clock.Advance(TimeSpan.FromHours(1));

        npi.Submit(Commercial, clock, Calendar);

        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.Assigned);
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.CommercialTimer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void AssignEngineering_sets_assignment_and_area_status()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);

        npi.AssignEngineering(Engineer.UserId, EngineeringAdmin, clock);

        npi.AssignedEngineeringUserId.Should().Be(Engineer.UserId);
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.Assigned);
    }

    [Fact]
    public void AssignDesign_does_not_require_engineering_to_be_assigned_first()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);

        var act = () => npi.AssignDesign(Designer.UserId, DesignAdmin, clock);

        act.Should().NotThrow();
        npi.AssignedDesignUserId.Should().Be(Designer.UserId);
    }

    [Fact]
    public void ReassignCommercial_updates_owner()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);
        var newOwner = Guid.NewGuid();

        npi.ReassignCommercial(newOwner, Commercial, clock);

        npi.AssignedCommercialUserId.Should().Be(newOwner);
    }

    [Fact]
    public void Assignments_are_rejected_once_the_npi_is_terminal()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);
        npi.RejectNoFeasibility(Engineer, clock, Calendar, null);

        var act = () => npi.AssignEngineering(Guid.NewGuid(), EngineeringAdmin, clock);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("npi-terminal");
    }
}
