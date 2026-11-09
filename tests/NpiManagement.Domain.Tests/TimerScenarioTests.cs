using FluentAssertions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>
/// Recorridos completos de los 4 cronómetros con el reloj avanzando dentro de horario hábil
/// (lunes 10:00 CDMX; cada paso es de 30 min para no salir del día). Cada prueba verifica quién
/// "carga" el tiempo en cada tramo, que es lo que ven los usuarios en el detalle del NPI.
/// </summary>
public class TimerScenarioTests
{
    private const long HalfHour = 30 * 60 * 1000;
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(30);

    [Fact]
    public void Happy_path_charges_each_stretch_to_the_right_area_and_closes_everything()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);                       // Global + Ingeniería corriendo; Diseño arranca aquí
        npi.DesignTimer.IsRunning.Should().BeTrue();

        clock.Advance(Step);                                            // Diseño trabaja el PrintCard
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(npi, clock), null);
        npi.DesignTimer.IsRunning.Should().BeFalse("Diseño espera la revisión de Ingeniería");
        npi.EngineeringTimer.IsRunning.Should().BeTrue("Ingeniería revisa el PrintCard");
        npi.CommercialTimer.IsRunning.Should().BeFalse("Comercial aún no tiene nada que llevar al cliente");

        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.CommercialTimer.IsRunning.Should().BeTrue("Comercial gestiona el PrintCard con el cliente");
        npi.SendPrintCardToClient(Commercial, clock);
        clock.Advance(Step);                                            // esperando al cliente
        npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, File(npi, clock, AttachmentContext.ClientApproval));
        npi.CommercialTimer.IsRunning.Should().BeFalse();
        npi.DesignTimer.IsRunning.Should().BeTrue("Diseño carga el PrintCard");

        clock.Advance(Step);
        npi.LoadPrintCardIntoSystem(Designer, clock, Calendar);
        npi.DesignTimer.IsClosed.Should().BeTrue();

        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-1", null);
        npi.RegisterSapOrder(Commercial, clock, Calendar, "4500012345");
        npi.RegisterEt(Engineer, clock);
        clock.Advance(Step);
        npi.LoadEtIntoSispro(Engineer, clock, Calendar);

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Completed);
        npi.GlobalTimer.AccumulatedBusinessMs.Should().Be(4 * HalfHour);
        npi.EngineeringTimer.AccumulatedBusinessMs.Should().Be(1 * HalfHour, "sólo cuenta el tramo PT/ET; la espera del PrintCard y del cliente no es de Ingeniería");
        npi.DesignTimer.AccumulatedBusinessMs.Should().Be(2 * HalfHour);
        npi.CommercialTimer.AccumulatedBusinessMs.Should().Be(1 * HalfHour);
        new[] { npi.GlobalTimer, npi.EngineeringTimer, npi.DesignTimer, npi.CommercialTimer }
            .Should().OnlyContain(t => t.IsClosed);
    }

    [Fact]
    public void Engineering_changes_pause_engineering_and_charge_commercial_until_resubmitted()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        clock.Advance(Step);
        npi.RequestEngineeringChanges(Engineer, clock, Calendar, "Corregir ancho", ["ancho"]);
        clock.Advance(Step);                                            // Comercial corrige
        npi.Submit(Commercial, clock, Calendar);
        clock.Advance(Step);

        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.CommercialTimer.IsRunning.Should().BeFalse();
        npi.CommercialTimer.AccumulatedBusinessMs.Should().Be(HalfHour);
    }

    [Fact]
    public void Client_changes_hand_the_clock_back_to_design_while_it_makes_the_new_version()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(npi, clock), null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, clock);

        clock.Advance(Step);                                            // esperando al cliente: Comercial
        npi.RequestClientChanges(Commercial, clock, Calendar, null, ["Cambiar color del logo"]);
        clock.Advance(Step);                                            // Diseño hace la v2
        var item = npi.ChangeRequests.Single(c => c.Origin == ChangeRequestOrigin.ClientReported).Checklist[0];
        npi.SetClientChangeItemDone(Designer, clock, item.Id, true);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(npi, clock), null);

        npi.CommercialTimer.AccumulatedBusinessMs.Should().Be(HalfHour, "el tiempo de rehacer la versión es de Diseño, no de Comercial");
        npi.DesignTimer.AccumulatedBusinessMs.Should().Be(HalfHour);
    }

    [Fact]
    public void Engineering_review_of_the_printcard_is_charged_to_engineering_and_a_rejection_goes_back_to_design()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(npi, clock), null);
        var engineeringBefore = npi.EngineeringTimer.AccumulatedBusinessMs;

        clock.Advance(Step);                                            // Ingeniería revisa
        npi.RejectPrintCard(Engineer, clock, Calendar, "Falta el código de barras", null);
        clock.Advance(Step);                                            // Diseño corrige

        npi.EngineeringTimer.AccumulatedBusinessMs.Should().Be(engineeringBefore + HalfHour);
        npi.EngineeringTimer.IsRunning.Should().BeFalse();
        npi.DesignTimer.IsRunning.Should().BeTrue();
        npi.CommercialTimer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Answering_an_information_request_does_not_start_design_before_its_preconditions()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);                          // sin factibilidad de Ingeniería ni artes

        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "¿Pantones?");
        clock.Advance(Step);
        npi.RespondCommercialInformation(Commercial, clock, Calendar, "Los del arte");

        npi.DesignTimer.HasStarted.Should().BeFalse("Diseño arranca con factibilidad de Ingeniería + artes validadas");
    }

    [Fact]
    public void Supplies_temporary_rejection_charges_commercial_until_the_agreement()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, "Sin película");
        clock.Advance(Step);                                            // Comercial negocia insumos
        npi.ResolveSuppliesAgreement(Commercial, clock, Calendar, "Proveedor alterno", Guid.NewGuid());

        npi.CommercialTimer.AccumulatedBusinessMs.Should().Be(HalfHour);
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
    }
    [Fact]
    public void Engineering_only_charges_the_stretches_where_it_has_work()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        clock.Advance(Step);                                            // 1) factibilidad: Ingeniería
        npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.A);
        npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);
        npi.EngineeringTimer.IsRunning.Should().BeFalse("ahora la pelota la tiene Comercial (cotizar)");
        npi.ValidateArtwork(Designer, clock);
        LinkQuote(npi, clock);                                          // cotización y aceptación al instante
        npi.AcceptSisproQuote(Commercial, clock, Calendar, null, null);

        clock.Advance(Step);                                            // Diseño trabaja el PrintCard (no es de Ingeniería)
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(npi, clock), null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, clock);
        clock.Advance(Step);                                            // esperando al cliente (no es de Ingeniería)
        npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, File(npi, clock, AttachmentContext.ClientApproval));
        npi.LoadPrintCardIntoSystem(Designer, clock, Calendar);
        npi.EngineeringTimer.IsRunning.Should().BeTrue("le toca registrar el PT");

        clock.Advance(Step);                                            // 2) alta del PT: Ingeniería
        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-1", null);
        npi.EngineeringTimer.IsRunning.Should().BeFalse("espera el Pedido de Comercial");

        clock.Advance(Step);                                            // Comercial registra el pedido (no es de Ingeniería)
        npi.RegisterSapOrder(Commercial, clock, Calendar, "4500012345");
        npi.EngineeringTimer.IsRunning.Should().BeTrue("le toca la ET");

        clock.Advance(Step);                                            // 3) ET: Ingeniería
        npi.RegisterEt(Engineer, clock);
        npi.LoadEtIntoSispro(Engineer, clock, Calendar);

        npi.EngineeringTimer.AccumulatedBusinessMs.Should().Be(3 * HalfHour);
        npi.CommercialTimer.AccumulatedBusinessMs.Should().Be(2 * HalfHour, "esperar al cliente + registrar el Pedido");
        npi.GlobalTimer.AccumulatedBusinessMs.Should().Be(6 * HalfHour);
    }

    [Fact]
    public void Resubmitting_changes_after_feasibility_does_not_restart_engineering_while_it_waits()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);                       // factibilidad de Ingeniería ya aprobada

        npi.RequestEngineeringChanges(Engineer, clock, Calendar, "Ajustar descripción", []);
        clock.Advance(Step);
        npi.Submit(Commercial, clock, Calendar);

        npi.EngineeringTimer.IsRunning.Should().BeFalse("sigue esperando el PrintCard");
    }
}
