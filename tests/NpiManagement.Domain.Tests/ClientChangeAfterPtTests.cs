using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>
/// Cambio del cliente después del PT (2026-11-12): Comercial modifica con el correo del cliente hasta antes de
/// la ET cargada; Ingeniería decide qué pasa con el PT y Diseño si el PrintCard sigue igual. Mientras tanto no
/// hay pedido ni ET; un pedido ya capturado no se bloquea.
/// </summary>
public class ClientChangeAfterPtTests
{
    private readonly TestClock _clock = TestClock.MondayMorning();

    private Npi WithPt()
    {
        var npi = CreateWithPrintCardLoaded(_clock);
        npi.RegisterSapPt(Engineer, _clock, Calendar, "PT12553", "RADICCHIO");
        return npi;
    }

    private Guid ClientEmail(Npi npi) => File(npi, _clock, AttachmentContext.ChangeRequest);

    /// <summary>Comercial abre la modificación, cambia la captura y reenvía.</summary>
    private void ClientChange(Npi npi, Func<NpiManagement.Domain.ValueObjects.NpiCaptureData, NpiManagement.Domain.ValueObjects.NpiCaptureData> change)
    {
        npi.StartCommercialEdit(Commercial, _clock, Calendar, "El cliente cambió el ancho", false, ClientEmail(npi));
        npi.UpdateDraft(Commercial, _clock, change(npi.Capture), npi.Priority);
        npi.Submit(Commercial, _clock, Calendar);
    }

    [Fact]
    public void After_the_pt_the_clients_email_is_required()
    {
        var npi = WithPt();

        FluentActions.Invoking(() => npi.StartCommercialEdit(Commercial, _clock, Calendar, "Cambio", false))
            .Should().Throw<NpiValidationException>().Which.Code.Should().Be("client-change-evidence-required");
    }

    [Fact]
    public void Not_allowed_once_the_et_is_loaded_in_sispro()
    {
        var npi = WithPt();
        npi.RegisterSapOrder(Commercial, _clock, Calendar, "4500123");
        npi.RegisterEt(Engineer, _clock);
        npi.LoadEtIntoSispro(Engineer, _clock, Calendar);

        // Ya completado: el NPI es terminal.
        FluentActions.Invoking(() => npi.StartCommercialEdit(Commercial, _clock, Calendar, "Cambio", false, Guid.NewGuid()))
            .Should().Throw<InvalidNpiTransitionException>();
    }

    [Fact]
    public void Changing_engineering_and_printcard_data_asks_both_areas_and_blocks_the_order()
    {
        var npi = WithPt();

        ClientChange(npi, c => c with { Ancho = "11" });

        npi.PendingEngineeringReconfirmation.Should().NotBeNull();
        npi.PendingDesignReconfirmation.Should().NotBeNull();
        npi.PendingDesignReconfirmation!.ChangesApplied.Should().ContainSingle(c => c.Field == "ancho");
        npi.ClientChangeUnderReview.Should().BeTrue();
        npi.DesignTimer.IsRunning.Should().BeTrue();
        FluentActions.Invoking(() => npi.RegisterSapOrder(Commercial, _clock, Calendar, "4500123"))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("client-change-under-review");
    }

    [Fact]
    public void Engineering_must_say_what_happens_with_the_pt()
    {
        var npi = WithPt();
        ClientChange(npi, c => c with { Ancho = "11" });

        FluentActions.Invoking(() => npi.ReconfirmEngineeringFeasibility(Engineer, _clock, Calendar, null))
            .Should().Throw<NpiValidationException>().Which.Code.Should().Be("pt-decision-required");
    }

    [Fact]
    public void Same_pt_and_same_printcard_go_back_to_the_order()
    {
        var npi = WithPt();
        ClientChange(npi, c => c with { Ancho = "11" });

        npi.ReconfirmEngineeringFeasibility(Engineer, _clock, Calendar, "Sólo cambia la ficha", PtDecision.UpdatePtSameCode);
        npi.ReconfirmDesignAfterClientChange(Designer, _clock, Calendar, false, null);

        npi.ClientChangeUnderReview.Should().BeFalse();
        npi.CodigoPT.Should().Be("PT12553");
        npi.PrintCardLoadedAt.Should().NotBeNull();
        npi.DesignTimer.IsClosed.Should().BeTrue();
        npi.CommercialTimer.IsRunning.Should().BeTrue();
        npi.RegisterSapOrder(Commercial, _clock, Calendar, "4500123");
        npi.PedidoSapNumber.Should().Be("4500123");
    }

    [Fact]
    public void New_pt_and_new_printcard_go_back_through_the_flow_keeping_the_old_pt_in_the_log()
    {
        var npi = WithPt();
        ClientChange(npi, c => c with { Ancho = "11" });

        npi.ReconfirmDesignAfterClientChange(Designer, _clock, Calendar, true, "Cambia la cota del ancho");
        npi.ReconfirmEngineeringFeasibility(Engineer, _clock, Calendar, null, PtDecision.NewPt);

        npi.CodigoPT.Should().BeNull();
        npi.PrintCardLoadedAt.Should().BeNull();
        npi.AuditLog.Should().Contain(e => e.Action == "SapPtVoided" && e.Details!.Contains("pt=PT12553"));
        npi.CurrentPrintCardVersion!.RejectionReason.Should().Be(Npi.PrintCardRetiredByClientChangeReason);

        // Flujo normal otra vez: versión nueva → Ingeniería → cliente → carga → PT nuevo → pedido.
        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0001_R-2", File(npi, _clock), null);
        npi.ApprovePrintCard(Engineer, _clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, _clock);
        npi.ApprovePrintCard(Commercial, _clock, Calendar, ApproverRole.Client, null, File(npi, _clock, AttachmentContext.ClientApproval));
        npi.LoadPrintCardIntoSystem(Designer, _clock, Calendar);
        npi.RegisterSapPt(Engineer, _clock, Calendar, "PT12999", null);
        npi.RegisterSapOrder(Commercial, _clock, Calendar, "4500123");

        npi.FinalPrintCardVersion!.Code.Should().Be("PC-0001_R-2");
        npi.CodigoPT.Should().Be("PT12999");
    }

    [Fact]
    public void A_captured_order_is_kept_when_a_new_pt_is_needed()
    {
        var npi = WithPt();
        npi.RegisterSapOrder(Commercial, _clock, Calendar, "4500123");
        npi.RegisterEt(Engineer, _clock);
        ClientChange(npi, c => c with { Ancho = "11" });

        npi.ReconfirmEngineeringFeasibility(Engineer, _clock, Calendar, null, PtDecision.NewPt);
        npi.ReconfirmDesignAfterClientChange(Designer, _clock, Calendar, false, null);

        npi.PedidoSapNumber.Should().Be("4500123");
        npi.EtCreatedAt.Should().BeNull("la ET se hizo con el PT anterior");
        npi.RegisterSapPt(Engineer, _clock, Calendar, "PT12999", null);
        npi.EngineeringTimer.IsRunning.Should().BeTrue("con el pedido ya capturado, Ingeniería sigue con la ET");
        npi.RegisterEt(Engineer, _clock);
        npi.LoadEtIntoSispro(Engineer, _clock, Calendar);
        npi.LifecycleStatus.Should().Be(LifecycleStatus.Completed);
    }

    [Fact]
    public void Only_contact_or_notes_changes_do_not_ask_anyone()
    {
        var npi = WithPt();

        ClientChange(npi, c => c with { Observaciones = "Entregar en el andén 3" });

        npi.PendingEngineeringReconfirmation.Should().BeNull();
        npi.PendingDesignReconfirmation.Should().BeNull();
        npi.ClientChangeUnderReview.Should().BeFalse();
        npi.RegisterSapOrder(Commercial, _clock, Calendar, "4500123");
    }

    [Fact]
    public void Before_the_pt_nothing_changes_no_evidence_needed()
    {
        var npi = CreateWithPrintCardLoaded(_clock);

        npi.StartCommercialEdit(Commercial, _clock, Calendar, "Cambio", false);

        npi.PendingCommercialEdit!.IsClientChangeAfterPt.Should().BeFalse();
    }
}
