using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class NpiFeasibilityAndArtworkTests
{
    [Fact]
    public void EngineeringFeasibility_requires_an_assigned_engineer()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock); // sin asignar

        var act = () => npi.ApproveEngineeringFeasibility(Engineer, clock, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("engineering-not-assigned");
    }

    [Fact]
    public void DesignFeasibility_can_be_approved_before_engineering_feasibility_decision_1()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        var act = () => npi.ApproveDesignFeasibility(Designer, clock, "Ok diseño");

        act.Should().NotThrow("las factibilidades son independientes y paralelas (decisión 1)");
        npi.DesignFeasibility.Should().NotBeNull();
        npi.EngineeringFeasibility.Should().BeNull();
    }

    [Fact]
    public void EngineeringFeasibility_can_be_approved_before_design_feasibility_decision_1()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        var act = () => npi.ApproveEngineeringFeasibility(Engineer, clock, "Ok ingeniería");

        act.Should().NotThrow("las factibilidades son independientes y paralelas (decisión 1)");
        npi.EngineeringFeasibility.Should().NotBeNull();
        npi.DesignFeasibility.Should().BeNull();
    }

    [Fact]
    public void Approving_the_same_area_feasibility_twice_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.ApproveEngineeringFeasibility(Engineer, clock, null);

        var act = () => npi.ApproveEngineeringFeasibility(Engineer, clock, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("engineering-feasibility-already-approved");
    }

    [Fact]
    public void DesignTimer_does_not_start_when_only_artwork_is_validated()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.ValidateArtwork(Designer, clock);

        npi.DesignTimer.HasStarted.Should().BeFalse("falta Factibilidad de Ingeniería (invariante 6)");
    }

    [Fact]
    public void DesignTimer_does_not_start_when_only_engineering_feasibility_is_approved()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.ApproveEngineeringFeasibility(Engineer, clock, null);

        npi.DesignTimer.HasStarted.Should().BeFalse("falta Artes Validadas (invariante 6)");
    }

    [Fact]
    public void DesignTimer_starts_when_engineering_feasibility_arrives_after_artwork_validated()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.ValidateArtwork(Designer, clock);

        npi.ApproveEngineeringFeasibility(Engineer, clock, null);

        npi.DesignTimer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void DesignTimer_starts_when_artwork_is_validated_after_engineering_feasibility_arrives()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.ApproveEngineeringFeasibility(Engineer, clock, null);

        npi.ValidateArtwork(Designer, clock);

        npi.DesignTimer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void ValidateArtwork_twice_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.ValidateArtwork(Designer, clock);

        var act = () => npi.ValidateArtwork(Designer, clock);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("artwork-already-validated");
    }

    [Fact]
    public void RequestEngineeringChanges_requires_description_or_field_keys()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        var act = () => npi.RequestEngineeringChanges(Engineer, clock, Calendar, "  ", []);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("change-request-empty");
    }

    [Fact]
    public void RequestEngineeringChanges_pauses_engineering_and_resumes_commercial()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.RequestEngineeringChanges(Engineer, clock, Calendar, "Faltan datos de cliente", ["cliente"]);

        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.ChangesRequested);
        npi.EngineeringTimer.IsRunning.Should().BeFalse();
        npi.CommercialTimer.IsRunning.Should().BeTrue();
        npi.ChangeRequests.Should().ContainSingle(cr => cr.FromArea == AreaType.Ingenieria && cr.ToArea == AreaType.Comercial);
    }

    [Fact]
    public void RejectNoFeasibility_is_terminal_and_closes_all_open_timers()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.RejectNoFeasibility(Engineer, clock, Calendar, "No es viable técnicamente");

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Rejected);
        npi.TerminalReason.Should().Be(TerminalReason.NoFeasibility);
        npi.EngineeringTimer.IsClosed.Should().BeTrue();
        npi.GlobalTimer.IsClosed.Should().BeTrue();
    }
}
