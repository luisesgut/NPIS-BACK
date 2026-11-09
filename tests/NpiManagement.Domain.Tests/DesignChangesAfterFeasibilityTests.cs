using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>
/// Diseño pide cambios a Comercial después de aprobar su factibilidad (p. ej. al hacer el PrintCard) y,
/// si Comercial toca campos de Ingeniería, Ingeniería reconfirma su factibilidad (2026-11-03).
/// </summary>
public class DesignChangesAfterFeasibilityTests
{
    private readonly TestClock _clock = TestClock.MondayMorning();

    private void CommercialEdits(Npi npi, Func<NpiManagement.Domain.ValueObjects.NpiCaptureData, NpiManagement.Domain.ValueObjects.NpiCaptureData> change) =>
        npi.UpdateDraft(Commercial, _clock, change(npi.Capture), npi.Priority);

    [Fact]
    public void Design_can_ask_for_changes_while_making_the_printcard_and_gets_it_back_where_it_was()
    {
        var npi = CreateReadyForPrintCard(_clock);

        npi.RequestDesignChanges(Designer, _clock, Calendar, "Falta el código del cliente", ["codigoProdCliente"]);
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.ChangesRequested);
        npi.DesignTimer.IsRunning.Should().BeFalse();
        npi.CommercialTimer.IsRunning.Should().BeTrue();

        CommercialEdits(npi, c => c with { Observaciones = "Código 123" });
        npi.Submit(Commercial, _clock, Calendar);

        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.ArtesValidadas, "regresa al paso de hacer el PrintCard, sin volver a pedir factibilidad");
        npi.DesignTimer.IsRunning.Should().BeTrue();
        npi.PendingEngineeringReconfirmation.Should().BeNull("observaciones no es un campo de Ingeniería");
    }

    [Fact]
    public void Changing_an_engineering_field_asks_engineering_to_reconfirm_and_blocks_sending_to_the_client()
    {
        var npi = CreateReadyForPrintCard(_clock);
        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0001", File(npi, _clock), null);
        npi.ApprovePrintCard(Engineer, _clock, Calendar, ApproverRole.Engineering, null, null);

        npi.RequestDesignChanges(Designer, _clock, Calendar, "El ancho no coincide con el arte", ["ancho"]);
        npi.CurrentPrintCardVersion!.IsRejected.Should().BeTrue("la versión se hizo con los datos de antes");

        CommercialEdits(npi, c => c with { Ancho = "32" });
        npi.Submit(Commercial, _clock, Calendar);

        var reconfirmation = npi.PendingEngineeringReconfirmation;
        reconfirmation.Should().NotBeNull();
        reconfirmation!.ChangesApplied.Should().ContainSingle(c => c.Field == "ancho" && c.After == "32");
        npi.EngineeringFeasibility.Should().NotBeNull("la factibilidad no se borra");
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.PrintCardChangesRequested, "Diseño rehace el PrintCard en paralelo");

        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0001", File(npi, _clock), null);
        ((Action)(() => npi.ApprovePrintCard(Engineer, _clock, Calendar, ApproverRole.Engineering, null, null)))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("engineering-reconfirmation-pending");

        npi.ReconfirmEngineeringFeasibility(Engineer, _clock, Calendar, "Sigue siendo factible");
        npi.PendingEngineeringReconfirmation.Should().BeNull();
        npi.ApprovePrintCard(Engineer, _clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, _clock);
    }

    [Fact]
    public void Engineering_approval_of_a_printcard_drops_when_engineering_fields_change_after_it()
    {
        var npi = CreateReadyForPrintCard(_clock);
        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0001", File(npi, _clock), null);
        npi.ApprovePrintCard(Engineer, _clock, Calendar, ApproverRole.Engineering, null, null);
        // Ingeniería pide un cambio después de aprobar (por ejemplo al reconfirmar): Comercial corrige.
        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "Calibre", ["piCalibre"]);
        CommercialEdits(npi, c => c with { PiCalibre = "60" });
        npi.Submit(Commercial, _clock, Calendar);

        npi.CurrentPrintCardVersion!.Approvals.Should().NotContain(a => a.Role == ApproverRole.Engineering);
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.FeasibilityApproved, "no regresa a Asignado");
        ((Action)(() => npi.SendPrintCardToClient(Commercial, _clock))).Should().Throw<InvalidNpiTransitionException>();
    }

    [Fact]
    public void Design_cannot_ask_for_changes_once_the_printcard_went_to_the_client()
    {
        var npi = CreateReadyForPrintCard(_clock);
        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0001", File(npi, _clock), null);
        npi.ApprovePrintCard(Engineer, _clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, _clock);

        ((Action)(() => npi.RequestDesignChanges(Designer, _clock, Calendar, "x", ["ancho"])))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-already-sent");
    }

    [Fact]
    public void Undoing_the_design_request_brings_back_the_retired_printcard()
    {
        var npi = CreateReadyForPrintCard(_clock);
        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0001", File(npi, _clock), null);
        _clock.Advance(TimeSpan.FromMinutes(5));
        npi.RequestDesignChanges(Designer, _clock, Calendar, "El ancho no coincide", ["ancho"]);

        npi.UndoLastAction(Designer, _clock, Calendar);

        npi.CurrentPrintCardVersion!.IsRejected.Should().BeFalse();
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.PrintCardInApproval);
    }

    [Fact]
    public void Reconfirming_without_pending_changes_fails()
    {
        var npi = CreateReadyForPrintCard(_clock);

        ((Action)(() => npi.ReconfirmEngineeringFeasibility(Engineer, _clock, Calendar, null)))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("no-pending-reconfirmation");
    }

    // ===== Ingeniería pide cambios después de su factibilidad (hasta antes del PT) =====

    [Fact]
    public void Engineering_change_that_affects_a_loaded_printcard_sends_it_back_to_design()
    {
        var npi = CreateWithPrintCardLoaded(_clock);

        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "El fuelle está mal", ["fuelles"], redoPrintCard: true);

        npi.PrintCardLoadedAt.Should().BeNull();
        npi.CurrentPrintCardVersion!.IsRejected.Should().BeTrue();
        npi.DesignTimer.IsClosed.Should().BeFalse("Diseño vuelve a tener trabajo");
        npi.DesignTimer.IsRunning.Should().BeFalse("pero espera a que Comercial corrija");
        ((Action)(() => npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0002", File(npi, _clock), null)))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("commercial-changes-pending");

        CommercialEdits(npi, c => c with { Fuelles = "4" });
        npi.Submit(Commercial, _clock, Calendar);

        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.FeasibilityApproved);
        npi.PendingEngineeringReconfirmation.Should().NotBeNull();
        npi.DesignTimer.IsRunning.Should().BeTrue("ahora sí rehace el PrintCard");
        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0002", File(npi, _clock), null);
    }

    [Fact]
    public void Engineering_change_that_does_not_affect_the_printcard_keeps_it_but_blocks_the_pt_until_reconfirmed()
    {
        var npi = CreateWithPrintCardLoaded(_clock);

        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "Tarima", ["dimTarimaBolsa"], redoPrintCard: false);
        npi.PrintCardLoadedAt.Should().NotBeNull();
        ((Action)(() => npi.RegisterSapPt(Engineer, _clock, Calendar, "PT-1", null))).Should().Throw<InvalidNpiTransitionException>();

        CommercialEdits(npi, c => c with { DimTarimaBolsa = "1.10 x 1.00" });
        npi.Submit(Commercial, _clock, Calendar);
        ((Action)(() => npi.RegisterSapPt(Engineer, _clock, Calendar, "PT-1", null))).Should().Throw<InvalidNpiTransitionException>();

        npi.ReconfirmEngineeringFeasibility(Engineer, _clock, Calendar, null);
        npi.RegisterSapPt(Engineer, _clock, Calendar, "PT-1", null);
    }

    [Fact]
    public void Engineering_cannot_ask_for_changes_once_the_pt_is_registered()
    {
        var npi = CreateWithPrintCardLoaded(_clock);
        npi.RegisterSapPt(Engineer, _clock, Calendar, "PT-1", null);

        ((Action)(() => npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "x", ["ancho"])))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("pt-already-registered");
    }

    [Fact]
    public void Undoing_the_engineering_request_puts_the_printcard_back_in_the_system()
    {
        var npi = CreateWithPrintCardLoaded(_clock);
        var loadedAt = npi.PrintCardLoadedAt;
        _clock.Advance(TimeSpan.FromMinutes(5));
        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "El fuelle está mal", ["fuelles"], redoPrintCard: true);

        npi.UndoLastAction(Engineer, _clock, Calendar);

        npi.PrintCardLoadedAt.Should().Be(loadedAt);
        npi.CurrentPrintCardVersion!.IsRejected.Should().BeFalse();
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.PrintCardLoaded);
        npi.DesignTimer.IsClosed.Should().BeTrue();
    }

    // ===== Comercial modifica el NPI por su cuenta =====

    [Fact]
    public void Commercial_edit_retires_the_printcard_asks_engineering_to_reconfirm_and_closes_the_pending_design_question()
    {
        var npi = CreateReadyForPrintCard(_clock);
        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0001", File(npi, _clock), null);
        npi.RequestInformation(Designer, _clock, Calendar, InformationRequestType.Commercial, "El cliente corrigió medidas?");

        npi.StartCommercialEdit(Commercial, _clock, Calendar, "El cliente corrigió medidas", redoPrintCard: true);
        npi.CurrentPrintCardVersion!.IsRejected.Should().BeTrue();
        ((Action)(() => npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0002", File(npi, _clock), null))).Should().Throw<InvalidNpiTransitionException>();

        CommercialEdits(npi, c => c with { Alto = "14", Ancho = "10" });
        npi.Submit(Commercial, _clock, Calendar);

        npi.PendingCommercialEdit.Should().BeNull();
        npi.ChangeRequests.Single(c => c.Origin == ChangeRequestOrigin.CommercialEdit).ChangesApplied.Should().HaveCount(2);
        npi.PendingEngineeringReconfirmation.Should().NotBeNull();
        npi.InformationRequests.Should().OnlyContain(r => r.Status == InformationRequestStatus.Resolved);
        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0002", File(npi, _clock), null);
    }

    [Fact]
    public void Commercial_edit_needs_a_reason_and_is_not_allowed_after_the_pt()
    {
        var npi = CreateWithPrintCardLoaded(_clock);
        ((Action)(() => npi.StartCommercialEdit(Commercial, _clock, Calendar, " ", false))).Should().Throw<NpiValidationException>();
        npi.RegisterSapPt(Engineer, _clock, Calendar, "PT-1", null);
        ((Action)(() => npi.StartCommercialEdit(Commercial, _clock, Calendar, "x", false)))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("pt-already-registered");
    }

    [Fact]
    public void Commercial_can_cancel_the_edit_with_undo_if_nobody_acted()
    {
        var npi = CreateWithPrintCardLoaded(_clock);
        _clock.Advance(TimeSpan.FromMinutes(5));
        npi.StartCommercialEdit(Commercial, _clock, Calendar, "El cliente corrigió medidas", redoPrintCard: true);

        npi.UndoLastAction(Commercial, _clock, Calendar);

        npi.PendingCommercialEdit.Should().BeNull();
        npi.PrintCardLoadedAt.Should().NotBeNull();
        npi.CurrentPrintCardVersion!.IsRejected.Should().BeFalse();
    }

    // ===== AdminTotal corrige qué área pidió la solicitud =====

    private static readonly NpiManagement.Domain.Common.Actor Admin = new(Guid.NewGuid(), "AdminTotal", "Admin Total");

    [Fact]
    public void Admin_moves_an_engineering_request_to_design_and_the_resubmit_goes_to_design()
    {
        var npi = CreateReadyForPrintCard(_clock);
        npi.RequestEngineeringChanges(Admin, _clock, Calendar, "Medidas", ["ancho", "alto"]);
        var request = npi.ChangeRequests.Single(c => !c.IsResolved);

        npi.CorrectChangeRequestArea(Admin, _clock, Calendar, request.Id, AreaType.Diseno, "Lo pidió Diseño, me equivoqué de tarjeta");

        request.FromArea.Should().Be(AreaType.Diseno);
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.FeasibilityApproved);
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.ChangesRequested);

        CommercialEdits(npi, c => c with { Ancho = "10", Alto = "14" });
        npi.Submit(Commercial, _clock, Calendar);
        request.IsResolved.Should().BeTrue();
        npi.PendingEngineeringReconfirmation.Should().NotBeNull("cambió medidas que Ingeniería ya había aprobado");
    }

    [Fact]
    public void Admin_can_move_it_back_to_engineering_and_commercial_can_still_resubmit()
    {
        var npi = CreateReadyForPrintCard(_clock);
        npi.RequestDesignChanges(Designer, _clock, Calendar, "Medidas", ["ancho"]);
        var request = npi.ChangeRequests.Single(c => !c.IsResolved);

        npi.CorrectChangeRequestArea(Admin, _clock, Calendar, request.Id, AreaType.Ingenieria, "Lo pidió Ingeniería");

        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.ChangesRequested);
        npi.DesignAreaStatus.Should().NotBe(DesignAreaStatus.ChangesRequested);
        CommercialEdits(npi, c => c with { Ancho = "10" });
        npi.Submit(Commercial, _clock, Calendar);
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.FeasibilityApproved);
    }

    [Fact]
    public void A_resolved_request_cannot_be_corrected()
    {
        var npi = CreateReadyForPrintCard(_clock);
        npi.RequestDesignChanges(Designer, _clock, Calendar, "Medidas", ["ancho"]);
        var request = npi.ChangeRequests.Single();
        npi.Submit(Commercial, _clock, Calendar);

        ((Action)(() => npi.CorrectChangeRequestArea(Admin, _clock, Calendar, request.Id, AreaType.Ingenieria, "x")))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("change-request-already-resolved");
    }



}
