using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class NpiSuppliesRejectionTests
{
    [Fact]
    public void RejectSuppliesTemporary_pauses_engineering_but_not_commercial_or_global()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, "Falta insumo X");

        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.SuppliesTemporaryRejection);
        npi.EngineeringTimer.IsRunning.Should().BeFalse();
        npi.GlobalTimer.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void RejectSuppliesTemporary_is_not_eligible_once_engineering_feasibility_is_already_approved()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.ApproveEngineeringFeasibility(Engineer, clock, null);

        var act = () => npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("supplies-rejection-not-eligible");
    }

    [Fact]
    public void RejectSuppliesTemporary_twice_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        var act = () => npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("supplies-rejection-already-open");
    }

    [Fact]
    public void ResolveSuppliesAgreement_requires_an_open_temporary_rejection()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        var act = () => npi.ResolveSuppliesAgreement(Commercial, clock, Calendar, "Se resuelve así", Guid.NewGuid());

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("no-open-supplies-rejection");
    }

    [Fact]
    public void ResolveSuppliesAgreement_requires_text()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        var act = () => npi.ResolveSuppliesAgreement(Commercial, clock, Calendar, "   ", Guid.NewGuid());

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("supplies-agreement-text-required");
    }

    [Fact]
    public void ResolveSuppliesAgreement_resumes_engineering_and_clears_the_temporary_rejection()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        npi.ResolveSuppliesAgreement(Commercial, clock, Calendar, "Se compra a otro proveedor", Guid.NewGuid());

        npi.SuppliesTemporaryRejection.Should().BeNull();
        npi.SuppliesAgreement.Should().NotBeNull();
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.Assigned);
    }

    [Fact]
    public void RejectSuppliesTotal_requires_an_open_temporary_rejection()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        var act = () => npi.RejectSuppliesTotal(Commercial, clock, Calendar);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("no-open-supplies-rejection");
    }

    [Fact]
    public void RejectSuppliesTotal_is_terminal_and_closes_every_running_timer()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "Pregunta"); // arranca CommercialTimer
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        npi.RejectSuppliesTotal(Commercial, clock, Calendar);

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Rejected);
        npi.TerminalReason.Should().Be(TerminalReason.SuppliesRejectionTotal);
        npi.GlobalTimer.IsClosed.Should().BeTrue();
        npi.EngineeringTimer.IsClosed.Should().BeTrue();
        npi.CommercialTimer.IsClosed.Should().BeTrue();
    }
}
