using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Etapa "Cotización SISPRO": ligar, respuesta del cliente al precio y su efecto en el PrintCard y los relojes.</summary>
public class NpiSisproQuoteTests
{
    private static readonly TimeSpan HalfHour = TimeSpan.FromMinutes(30);

    [Fact]
    public void A_quote_cannot_be_linked_before_engineering_approves_the_structure()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        var act = () => LinkQuote(npi, clock);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("sispro-quote-before-feasibility");
    }

    [Fact]
    public void Engineering_feasibility_hands_the_clock_to_commercial_to_quote()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);

        npi.CommercialTimer.IsRunning.Should().BeTrue("a Comercial le toca cotizar en SISPRO");
    }

    [Fact]
    public void Linking_a_quote_waits_for_the_client_and_keeps_the_structure_differences()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);

        LinkQuote(npi, clock, "678", ["1ª laminación: se cotizó a 75 micras y el NPI dice 62.5."]);

        var quote = npi.CurrentSisproQuote!;
        quote.VersionNumber.Should().Be(1);
        quote.NumeroCotizacion.Should().Be("678");
        quote.Status.Should().Be(SisproQuoteStatus.AwaitingClient);
        quote.StructureWarnings.Should().ContainSingle();
        npi.CommercialTimer.IsRunning.Should().BeTrue("el cliente todavía no responde al precio");
    }

    [Fact]
    public void Linking_another_quote_while_the_client_has_not_answered_replaces_the_previous_one()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);
        var first = LinkQuote(npi, clock, "678");

        var second = LinkQuote(npi, clock, "690");

        first.Status.Should().Be(SisproQuoteStatus.Superseded);
        second.VersionNumber.Should().Be(2);
        npi.CurrentSisproQuote.Should().BeSameAs(second);
    }

    [Fact]
    public void Accepting_the_price_starts_design_and_pauses_commercial()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);
        LinkQuote(npi, clock);
        clock.Advance(HalfHour);

        npi.AcceptSisproQuote(Commercial, clock, Calendar, "Aceptó por correo", File(npi, clock, AttachmentContext.Comment));

        npi.SisproQuoteAccepted.Should().BeTrue();
        npi.CurrentSisproQuote!.ResponseNote.Should().Be("Aceptó por correo");
        npi.DesignTimer.IsRunning.Should().BeTrue();
        npi.CommercialTimer.IsRunning.Should().BeFalse();
        npi.CommercialTimer.AccumulatedBusinessMs.Should().Be((long)HalfHour.TotalMilliseconds, "cotizar y esperar al cliente es tiempo de Comercial");
    }

    [Fact]
    public void Once_accepted_no_other_quote_can_be_linked()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        var act = () => LinkQuote(npi, clock, "999");

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("sispro-quote-already-accepted");
    }

    [Fact]
    public void An_adjustment_needs_a_note_and_then_a_new_quote_is_linked()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);
        LinkQuote(npi, clock, "678");

        var withoutNote = () => npi.RequestSisproQuoteAdjustment(Commercial, clock, "  ");
        withoutNote.Should().Throw<NpiValidationException>().Which.Code.Should().Be("sispro-quote-adjustment-note-required");

        npi.RequestSisproQuoteAdjustment(Commercial, clock, "Precio objetivo $2,500 el millar");
        npi.CurrentSisproQuote!.Status.Should().Be(SisproQuoteStatus.AdjustmentRequested);

        var accept = () => npi.AcceptSisproQuote(Commercial, clock, Calendar, null, null);
        accept.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("sispro-quote-not-awaiting-client");

        LinkQuote(npi, clock, "701");
        npi.CurrentSisproQuote!.VersionNumber.Should().Be(2);
        npi.CurrentSisproQuote.Status.Should().Be(SisproQuoteStatus.AwaitingClient);
    }

    [Fact]
    public void The_client_cannot_reject_a_price_it_has_not_seen()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);

        var act = () => npi.RejectByPrice(Commercial, clock, Calendar, "Muy caro", null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("no-sispro-quote");
    }

    [Fact]
    public void Rejecting_by_price_is_terminal_and_closes_all_timers()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);
        LinkQuote(npi, clock);

        var withoutComment = () => npi.RejectByPrice(Commercial, clock, Calendar, "", null);
        withoutComment.Should().Throw<NpiValidationException>().Which.Code.Should().Be("price-rejection-comment-required");

        npi.RejectByPrice(Commercial, clock, Calendar, "El cliente se fue con otro proveedor", null);

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Rejected);
        npi.TerminalReason.Should().Be(TerminalReason.PriceRejection);
        npi.CurrentSisproQuote!.Status.Should().Be(SisproQuoteStatus.Rejected);
        npi.GlobalTimer.IsClosed.Should().BeTrue();
        npi.CommercialTimer.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void The_first_printcard_waits_for_the_client_to_accept_the_price()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);
        LinkQuote(npi, clock);

        var act = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(npi, clock), null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("sispro-quote-not-accepted");
    }

    [Fact]
    public void Changing_the_quoted_structure_invalidates_the_quote()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        npi.UpdateDraft(Commercial, clock, npi.Capture with { PplCalibre = "62.5" }, npi.Priority);

        npi.CurrentSisproQuote!.Status.Should().Be(SisproQuoteStatus.Invalidated);
        npi.CurrentSisproQuote.InvalidationReason.Should().Contain("pplCalibre");
        npi.SisproQuoteAccepted.Should().BeFalse();
        var printCard = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(npi, clock), null);
        printCard.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("sispro-quote-not-accepted");

        LinkQuote(npi, clock, "702");                                   // se recotiza
        npi.CurrentSisproQuote!.VersionNumber.Should().Be(2);
    }

    [Fact]
    public void Changes_that_do_not_touch_the_quoted_structure_keep_the_quote()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        npi.UpdateDraft(Commercial, clock, npi.Capture with { ContactoCompras = "Compras Cliente" }, npi.Priority);

        npi.SisproQuoteAccepted.Should().BeTrue();
    }

    [Fact]
    public void Once_a_printcard_exists_an_invalidated_quote_does_not_stop_new_versions()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(npi, clock), null);
        npi.RejectPrintCard(Engineer, clock, Calendar, "Falta el código de barras", null);
        npi.UpdateDraft(Commercial, clock, npi.Capture with { PiCalibre = "15" }, npi.Priority);

        var act = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-2", File(npi, clock), null);

        act.Should().NotThrow("el PrintCard ya había arrancado; la recotización se ve en la etapa, no frena a Diseño");
    }

    [Fact]
    public void Accept_rejects_evidence_from_another_npi()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);
        LinkQuote(npi, clock);

        var act = () => npi.AcceptSisproQuote(Commercial, clock, Calendar, null, Guid.NewGuid());

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("evidence-not-found");
    }

    // ===== Etapa apagada (Sispro:CotizacionObligatoria=false) =====

    [Fact]
    public void With_the_stage_off_the_flow_is_the_old_one()
    {
        using var _ = SisproQuoteGate.Override(false);
        var clock = TestClock.MondayMorning();

        var npi = CreateFeasibleAwaitingQuote(clock);

        npi.CommercialTimer.HasStarted.Should().BeFalse("nadie espera una cotización");
        npi.DesignTimer.IsRunning.Should().BeTrue();
        npi.Invoking(n => n.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(n, clock), null)).Should().NotThrow();
    }

    [Fact]
    public void With_the_stage_off_a_quote_can_still_be_linked_and_accepted()
    {
        using var _ = SisproQuoteGate.Override(false);
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);

        LinkQuote(npi, clock);
        npi.AcceptSisproQuote(Commercial, clock, Calendar, null, null);

        npi.SisproQuoteAccepted.Should().BeTrue();
    }

    [Fact]
    public void Turning_the_stage_off_releases_an_npi_that_was_waiting_for_the_quote()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);
        npi.CommercialTimer.IsRunning.Should().BeTrue();
        npi.DesignTimer.HasStarted.Should().BeFalse();
        clock.Advance(HalfHour);

        using var _ = SisproQuoteGate.Override(false);
        npi.ReleaseFromSisproQuoteWait(clock, Calendar).Should().BeTrue();

        npi.CommercialTimer.IsRunning.Should().BeFalse();
        npi.DesignTimer.IsRunning.Should().BeTrue();
        npi.ReleaseFromSisproQuoteWait(clock, Calendar).Should().BeFalse("ya no hay nada que soltar");
        npi.Invoking(n => n.CreatePrintCardVersion(Designer, clock, Calendar, "PC-1", File(n, clock), null)).Should().NotThrow();
    }

    [Fact]
    public void Release_does_nothing_while_the_stage_is_on()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateFeasibleAwaitingQuote(clock);

        npi.ReleaseFromSisproQuoteWait(clock, Calendar).Should().BeFalse();
        npi.CommercialTimer.IsRunning.Should().BeTrue();
    }
}
