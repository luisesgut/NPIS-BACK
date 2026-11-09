using FluentAssertions;
using NpiManagement.Application.Npis.Turns;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>"Tu turno": a quién le toca qué en cada momento del flujo (2026-11-03).</summary>
public sealed class NpiTurnsTests
{
    private static TurnFacts Active(bool engineer = true, bool designer = true) => new(
        LifecycleStatus.Active, engineer, designer, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false);

    private static IEnumerable<string> ActionsOf(TurnFacts f, AreaType area) => NpiTurns.For(f).Where(t => t.Area == area).Select(t => t.Action);

    [Fact]
    public void A_draft_waits_only_for_commercial()
    {
        var turns = NpiTurns.For(Active() with { Lifecycle = LifecycleStatus.Draft });
        turns.Should().ContainSingle().Which.Should().Be(new AreaTurn(AreaType.Comercial, NpiTurns.CompleteDraft));
    }

    [Fact]
    public void A_new_npi_waits_for_both_feasibilities_or_for_assignment()
    {
        ActionsOf(Active(), AreaType.Ingenieria).Should().Equal(NpiTurns.ReviewEngineeringFeasibility);
        ActionsOf(Active(), AreaType.Diseno).Should().Equal(NpiTurns.ReviewDesignFeasibility);
        ActionsOf(Active(engineer: false, designer: false), AreaType.Ingenieria).Should().Equal(NpiTurns.AssignEngineer);
        ActionsOf(Active(engineer: false, designer: false), AreaType.Diseno).Should().Equal(NpiTurns.AssignDesigner);
    }

    [Fact]
    public void Requested_changes_move_the_ball_to_commercial()
    {
        var f = Active() with { PendingEngineeringChanges = true };
        ActionsOf(f, AreaType.Comercial).Should().Equal(NpiTurns.AttendEngineeringChanges);
        ActionsOf(f, AreaType.Ingenieria).Should().BeEmpty();
    }

    [Fact]
    public void The_printcard_goes_engineering_then_commercial_then_design()
    {
        var ready = Active() with { EngineeringFeasibility = true, DesignFeasibility = true, ArtesValidadas = true };
        ActionsOf(ready, AreaType.Diseno).Should().Equal(NpiTurns.MakePrintCard);

        var inReview = ready with { HasPrintCard = true };
        ActionsOf(inReview, AreaType.Ingenieria).Should().Equal(NpiTurns.ReviewPrintCard);
        ActionsOf(inReview, AreaType.Diseno).Should().BeEmpty();

        var approved = inReview with { PrintCardEngineeringApproved = true };
        ActionsOf(approved, AreaType.Comercial).Should().Equal(NpiTurns.SendPrintCardToClient);
        ActionsOf(approved with { PrintCardSentToClient = true }, AreaType.Comercial).Should().Equal(NpiTurns.RegisterClientResponse);

        var clientOk = approved with { PrintCardSentToClient = true, PrintCardClientApproved = true };
        ActionsOf(clientOk, AreaType.Diseno).Should().Equal(NpiTurns.LoadPrintCard);
        ActionsOf(inReview with { PrintCardRejected = true }, AreaType.Diseno).Should().Equal(NpiTurns.FixPrintCard);
    }

    [Fact]
    public void After_loading_the_printcard_comes_pt_sap_order_and_et()
    {
        var loaded = Active() with { EngineeringFeasibility = true, DesignFeasibility = true, ArtesValidadas = true, HasPrintCard = true, PrintCardEngineeringApproved = true, PrintCardClientApproved = true, PrintCardLoaded = true };
        ActionsOf(loaded, AreaType.Ingenieria).Should().Equal(NpiTurns.RegisterPt);
        ActionsOf(loaded with { PtRegistered = true }, AreaType.Comercial).Should().Equal(NpiTurns.RegisterSapOrder);
        ActionsOf(loaded with { PtRegistered = true, SapOrderRegistered = true }, AreaType.Ingenieria).Should().Equal(NpiTurns.CreateEt);
        ActionsOf(loaded with { PtRegistered = true, SapOrderRegistered = true, EtCreated = true }, AreaType.Ingenieria).Should().Equal(NpiTurns.LoadEtInSispro);
    }

    [Fact]
    public void Reconfirmation_is_engineering_turn_and_design_waits_while_commercial_fixes_its_changes()
    {
        var making = Active() with { EngineeringFeasibility = true, DesignFeasibility = true, ArtesValidadas = true };

        ActionsOf(making with { PendingDesignChanges = true }, AreaType.Diseno).Should().BeEmpty("Comercial está corrigiendo lo que pidió Diseño");
        ActionsOf(making with { PendingDesignChanges = true }, AreaType.Comercial).Should().Equal(NpiTurns.AttendDesignChanges);

        ActionsOf(making with { PendingEngineeringReconfirmation = true }, AreaType.Ingenieria).Should().Equal(NpiTurns.ReconfirmFeasibility);
        ActionsOf(making with { PendingEngineeringReconfirmation = true }, AreaType.Diseno).Should().Equal(NpiTurns.MakePrintCard); // Diseño sigue en paralelo
        ActionsOf(making with { PendingEngineeringReconfirmation = true, PendingEngineeringChanges = true }, AreaType.Ingenieria).Should().BeEmpty();
    }

    [Fact]
    public void Design_questions_to_engineering_move_the_ball_to_engineering()
    {
        var making = Active() with { EngineeringFeasibility = true, DesignFeasibility = true, ArtesValidadas = true };

        ActionsOf(making with { PendingEngineeringInfo = true }, AreaType.Ingenieria).Should().Equal(NpiTurns.AnswerDesignQuestions);
        ActionsOf(making with { PendingEngineeringInfo = true }, AreaType.Diseno).Should().BeEmpty("Diseño espera la respuesta con su reloj en pausa");
        ActionsOf(making with { PendingCommercialInfo = true }, AreaType.Diseno).Should().BeEmpty();
        ActionsOf(making, AreaType.Diseno).Should().Equal(NpiTurns.MakePrintCard);
    }

}
