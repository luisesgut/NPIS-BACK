using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class NpiSapAndCycleCompletionTests
{
    [Fact]
    public void RegisterSapPt_requires_printcard_loaded_first()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithFullyApprovedPrintCard(clock); // aprobado pero aún no cargado

        var act = () => npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-not-loaded");
    }

    [Fact]
    public void RegisterSapOrder_requires_pt_registered_first()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);

        var act = () => npi.RegisterSapOrder(Commercial, clock, Calendar, "4500012345");

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("pt-not-registered");
    }

    [Fact]
    public void RegisterEt_requires_pt_and_pedido_sap_registered()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);
        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", "Nombre PT");

        var act = () => npi.RegisterEt(Engineer, clock);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("et-preconditions-not-met");
    }

    [Fact]
    public void LoadEtIntoSispro_requires_et_created_first_decision_9()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);
        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", null);
        npi.RegisterSapOrder(Commercial, clock, Calendar, "4500012345");

        var act = () => npi.LoadEtIntoSispro(Engineer, clock, Calendar);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("et-not-created");
    }

    [Fact]
    public void RegisterEt_alone_does_not_complete_the_cycle_decision_9()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);
        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", null);
        npi.RegisterSapOrder(Commercial, clock, Calendar, "4500012345");

        npi.RegisterEt(Engineer, clock);

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Active);
        npi.EtCreatedAt.Should().NotBeNull();
        npi.EtLoadedInSisproAt.Should().BeNull();
    }

    [Fact]
    public void Full_happy_path_completes_the_cycle_only_when_the_four_milestones_exist_decision_4()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock); // 1) PrintCard cargado
        npi.LifecycleStatus.Should().Be(LifecycleStatus.Active);

        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", "Nombre PT"); // 2) PT en SAP
        npi.LifecycleStatus.Should().Be(LifecycleStatus.Active);

        npi.RegisterSapOrder(Commercial, clock, Calendar, "4500012345"); // 3) Pedido SAP
        npi.LifecycleStatus.Should().Be(LifecycleStatus.Active);

        npi.RegisterEt(Engineer, clock);
        npi.LifecycleStatus.Should().Be(LifecycleStatus.Active, "ET Creada no completa el ciclo, sólo ET en SisPro (decisión 9)");

        npi.LoadEtIntoSispro(Engineer, clock, Calendar); // 4) ET en SisPro -> completa

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Completed);
    }

    [Fact]
    public void GlobalTimer_stays_open_through_all_intermediate_steps_and_closes_only_on_completion_decision_5()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);
        npi.GlobalTimer.IsClosed.Should().BeFalse();

        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", null);
        npi.GlobalTimer.IsClosed.Should().BeFalse();

        npi.RegisterSapOrder(Commercial, clock, Calendar, "4500012345");
        npi.GlobalTimer.IsClosed.Should().BeFalse();

        npi.RegisterEt(Engineer, clock);
        npi.GlobalTimer.IsClosed.Should().BeFalse();

        npi.LoadEtIntoSispro(Engineer, clock, Calendar);
        npi.GlobalTimer.IsClosed.Should().BeTrue("el ciclo se completó — único otro cierre posible es un rechazo terminal");
    }

    [Fact]
    public void LoadEtIntoSispro_closes_engineering_timer_regardless_of_whether_the_cycle_completes()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);
        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", null);
        npi.RegisterSapOrder(Commercial, clock, Calendar, "4500012345");
        npi.RegisterEt(Engineer, clock);

        npi.LoadEtIntoSispro(Engineer, clock, Calendar);

        npi.EngineeringTimer.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Actions_are_rejected_once_the_cycle_is_completed()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);
        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", null);
        npi.RegisterSapOrder(Commercial, clock, Calendar, "4500012345");
        npi.RegisterEt(Engineer, clock);
        npi.LoadEtIntoSispro(Engineer, clock, Calendar);

        var act = () => npi.RegisterSapPt(Engineer, clock, Calendar, "PT-002", null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("npi-terminal");
    }
}
