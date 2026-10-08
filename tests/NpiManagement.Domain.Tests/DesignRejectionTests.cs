using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Rechazos de Diseño: con cambios (ida y vuelta con Comercial), no factible (terminal) y artes rechazadas.</summary>
public class DesignRejectionTests
{
    private readonly TestClock _clock = TestClock.MondayMorning();

    // ===== Rechazar con cambios =====

    [Fact]
    public void Design_changes_go_to_commercial_and_start_its_timer_without_stopping_engineering()
    {
        var npi = CreateActiveAssigned(_clock);

        npi.RequestDesignChanges(Designer, _clock, Calendar, "El logo no corresponde al tipo de impresión", ["tipoImpresion"]);

        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.ChangesRequested);
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.Assigned, "Ingeniería sigue en paralelo");
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.CommercialTimer.IsRunning.Should().BeTrue();
        npi.ChangeRequests.Should().ContainSingle(c => c.FromArea == AreaType.Diseno && c.ToArea == AreaType.Comercial && c.Origin == ChangeRequestOrigin.Internal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Every_design_rejection_requires_a_note(string note)
    {
        var npi = CreateActiveAssigned(_clock);

        ((Action)(() => npi.RequestDesignChanges(Designer, _clock, Calendar, note, ["ancho"]))).Should().Throw<NpiValidationException>().Which.Code.Should().Be("design-rejection-note-required");
        ((Action)(() => npi.RejectDesignNoFeasibility(Designer, _clock, Calendar, note))).Should().Throw<NpiValidationException>();
        ((Action)(() => npi.RejectArtwork(Designer, _clock, Calendar, note, null))).Should().Throw<NpiValidationException>();
    }

    [Fact]
    public void Resubmitting_resolves_the_design_request_records_what_changed_and_returns_to_design()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.RequestDesignChanges(Designer, _clock, Calendar, "Corregir tipo de tinta", ["tipoTinta"]);
        _clock.Advance(TimeSpan.FromHours(1));

        npi.UpdateDraft(npi.Capture with { TipoTinta = "FRENTE + BARNIZ" }, npi.Priority);
        npi.Submit(Commercial, _clock, Calendar, comment: "Corregí la tinta");

        var request = npi.ChangeRequests.Single(c => c.FromArea == AreaType.Diseno);
        request.IsResolved.Should().BeTrue();
        request.ResponseComment.Should().Be("Corregí la tinta");
        request.ChangesApplied.Should().ContainSingle(c => c.Field == "tipoTinta" && c.After == "FRENTE + BARNIZ");
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.Assigned);
        npi.CommercialTimer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void One_resubmission_answers_engineering_and_design_at_once()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "Falta el ancho", ["ancho"]);
        npi.RequestDesignChanges(Designer, _clock, Calendar, "Falta el pantone", ["basePantone"]);

        npi.Submit(Commercial, _clock, Calendar, comment: "Listo ambos");

        npi.ChangeRequests.Should().OnlyContain(c => c.IsResolved);
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.Assigned);
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.Assigned);
    }

    [Fact]
    public void Commercial_timer_keeps_running_while_something_else_still_waits_for_commercial()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.RequestDesignChanges(Designer, _clock, Calendar, "Corregir medidas", ["ancho"]);
        npi.RejectArtwork(Designer, _clock, Calendar, "Las artes vienen en baja resolución", null);

        npi.Submit(Commercial, _clock, Calendar);

        npi.CommercialTimer.IsRunning.Should().BeTrue("las artes rechazadas siguen esperando a Comercial");
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.ArtworkRejected);
    }

    [Fact]
    public void Design_cannot_approve_its_feasibility_while_its_changes_are_pending()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.RequestDesignChanges(Designer, _clock, Calendar, "Corregir", ["ancho"]);

        var act = () => npi.ApproveDesignFeasibility(Designer, _clock, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("design-changes-pending");
    }

    [Fact]
    public void Design_cannot_reject_after_approving_its_feasibility()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.ApproveDesignFeasibility(Designer, _clock, null);

        ((Action)(() => npi.RequestDesignChanges(Designer, _clock, Calendar, "x", []))).Should().Throw<InvalidNpiTransitionException>();
        ((Action)(() => npi.RejectDesignNoFeasibility(Designer, _clock, Calendar, "x"))).Should().Throw<InvalidNpiTransitionException>();
    }

    [Fact]
    public void Design_rejections_require_a_submitted_npi()
    {
        var npi = CreateDraft(_clock);
        npi.AssignDesign(Designer.UserId, DesignAdmin, _clock);

        var act = () => npi.RequestDesignChanges(Designer, _clock, Calendar, "Corregir", []);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("npi-not-active");
    }

    // ===== No factible =====

    [Fact]
    public void Design_no_feasibility_closes_the_npi()
    {
        var npi = CreateActiveAssigned(_clock);

        npi.RejectDesignNoFeasibility(Designer, _clock, Calendar, "No se puede imprimir ese degradado en flexografía");

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Rejected);
        npi.TerminalReason.Should().Be(TerminalReason.DesignNoFeasibility);
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.NoFeasibility);
        npi.GlobalTimer.IsClosed.Should().BeTrue();
        npi.AuditLog.Should().Contain(e => e.Action == "NpiRejectedDesignNoFeasibility" && e.Details!.Contains("degradado"));
    }

    // ===== Artes rechazadas =====

    [Fact]
    public void Rejected_artwork_goes_back_to_commercial_and_blocks_validation()
    {
        var npi = CreateActiveAssigned(_clock);

        npi.RejectArtwork(Designer, _clock, Calendar, "Faltan los textos legales", null);

        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.ArtworkRejected);
        npi.CommercialTimer.IsRunning.Should().BeTrue();
        npi.ChangeRequests.Should().ContainSingle(c => c.Origin == ChangeRequestOrigin.ArtworkRejected && c.Description == "Faltan los textos legales");
        ((Action)(() => npi.ValidateArtwork(Designer, _clock))).Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("artwork-rejection-pending");
    }

    [Fact]
    public void Commercial_must_upload_new_artwork_before_returning_it()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.RegisterLink(Commercial, _clock, "Artes v1", "https://drive.example.com/v1", AttachmentContext.Artwork);
        _clock.Advance(TimeSpan.FromMinutes(10));
        npi.RejectArtwork(Designer, _clock, Calendar, "Faltan los textos legales", null);

        var withoutNewArt = () => npi.ResolveArtworkRejection(Commercial, _clock, Calendar, null);
        withoutNewArt.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("new-artwork-required");

        _clock.Advance(TimeSpan.FromMinutes(30));
        npi.RegisterLink(Commercial, _clock, "Artes v2", "https://drive.example.com/v2", AttachmentContext.Artwork);
        npi.ResolveArtworkRejection(Commercial, _clock, Calendar, "Ya vienen con textos legales");

        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.Assigned);
        npi.CommercialTimer.IsRunning.Should().BeFalse();
        npi.ChangeRequests.Single(c => c.Origin == ChangeRequestOrigin.ArtworkRejected).ResponseComment.Should().Be("Ya vienen con textos legales");
        npi.Invoking(n => n.ValidateArtwork(Designer, _clock)).Should().NotThrow();
    }

    [Fact]
    public void Artwork_rejection_evidence_must_belong_to_the_npi()
    {
        var npi = CreateActiveAssigned(_clock);

        var act = () => npi.RejectArtwork(Designer, _clock, Calendar, "Baja resolución", Guid.NewGuid());

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("evidence-not-found");
    }

    // ===== Solicitudes de información (bug: Diseño se quedaba en "Esperando información") =====

    [Fact]
    public void Answering_an_information_request_restores_the_design_status()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.ApproveDesignFeasibility(Designer, _clock, null);
        npi.RequestInformation(Designer, _clock, Calendar, InformationRequestType.Commercial, "¿Pantone exacto?");

        npi.RespondCommercialInformation(Commercial, _clock, Calendar, "485 C");

        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.FeasibilityApproved);
    }
}
