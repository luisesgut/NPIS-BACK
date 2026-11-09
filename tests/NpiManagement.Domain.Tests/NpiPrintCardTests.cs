using FluentAssertions;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Entities;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class NpiPrintCardTests
{
    /// <summary>v1 generada y aprobada por Ingeniería: lista para que Comercial la envíe al cliente.</summary>
    private static Npi EngineeringApproved(IClock clock)
    {
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        return npi;
    }

    /// <summary>v1 enviada al cliente, que pidió estos cambios.</summary>
    private static Npi WithClientChanges(IClock clock, params string[] items)
    {
        var npi = EngineeringApproved(clock);
        npi.SendPrintCardToClient(Commercial, clock);
        npi.RequestClientChanges(Commercial, clock, Calendar, null, items);
        return npi;
    }

    private static ChangeRequest ClientRequest(Npi npi) => npi.ChangeRequests.Single(c => c.Origin == ChangeRequestOrigin.ClientReported);

    [Theory]
    [InlineData(false, true, true, true, "design-not-assigned")]
    [InlineData(true, false, true, true, "engineering-feasibility-missing")]
    [InlineData(true, true, false, true, "design-feasibility-missing")]
    [InlineData(true, true, true, false, "artwork-not-validated")]
    public void CreatePrintCardVersion_requires_all_four_preconditions_decision_2(
        bool assignDesign, bool engFeasible, bool designFeasible, bool artworkValidated, string expectedCode)
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);
        npi.AssignEngineering(Engineer.UserId, EngineeringAdmin, clock);
        if (assignDesign) npi.AssignDesign(Designer.UserId, DesignAdmin, clock);
        if (engFeasible) npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);
        if (designFeasible && assignDesign) npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.A);
        if (artworkValidated && assignDesign) npi.ValidateArtwork(Designer, clock);

        var act = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void CreatePrintCardVersion_requires_the_printcard_file()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        var sinArchivo = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", null, null);
        var archivoAjeno = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", Guid.NewGuid(), null);

        sinArchivo.Should().Throw<NpiValidationException>().Which.Code.Should().Be("printcard-file-required");
        archivoAjeno.Should().Throw<NpiValidationException>().Which.Code.Should().Be("evidence-not-found");
    }

    [Fact]
    public void CreatePrintCardVersion_records_design_approval_and_hands_the_review_to_engineering()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);

        npi.CurrentPrintCardVersion!.VersionNumber.Should().Be(1);
        npi.CurrentPrintCardVersion.Approvals.Should().ContainSingle(a => a.Role == ApproverRole.Design && a.ByUserId == Designer.UserId,
            "quien genera la versión ya está de acuerdo con ella");
        npi.EngineeringTimer.IsRunning.Should().BeTrue("le toca revisar a Ingeniería");
        npi.DesignTimer.IsRunning.Should().BeFalse();
        npi.CommercialTimer.IsRunning.Should().BeFalse("Comercial no tiene nada que hacer hasta que Ingeniería apruebe");
    }

    [Fact]
    public void A_second_version_is_not_allowed_while_the_current_one_is_in_review()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);

        var act = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0002", File(npi, clock), null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-version-in-review");
    }

    [Fact]
    public void CurrentPrintCardVersion_is_the_highest_version_even_if_the_collection_loads_out_of_order()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);
        npi.RejectPrintCard(Engineer, clock, Calendar, "Falta el código de barras", null);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0002", File(npi, clock), null);

        // EF Core puede materializar la colección en cualquier orden (llaves GUID).
        var versiones = (List<PrintCardVersion>)typeof(Npi)
            .GetField("_printCardVersions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(npi)!;
        versiones.Reverse();

        npi.CurrentPrintCardVersion!.VersionNumber.Should().Be(2);
        npi.CurrentPrintCardVersion.Code.Should().Be("PC-0002");
    }

    [Fact]
    public void CreatePrintCardVersion_requires_a_non_empty_code()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        var act = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "  ", File(npi, clock), null);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("printcard-code-required");
    }

    [Fact]
    public void Commercial_cannot_send_to_the_client_until_engineering_approves()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);

        var act = () => npi.SendPrintCardToClient(Commercial, clock);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-engineering-approval-missing");
    }

    [Fact]
    public void Engineering_approval_hands_the_printcard_to_commercial()
    {
        var clock = TestClock.MondayMorning();

        var npi = EngineeringApproved(clock);

        npi.EngineeringTimer.IsRunning.Should().BeFalse();
        npi.CommercialTimer.IsRunning.Should().BeTrue("ahora Comercial lo lleva al cliente");
    }

    [Fact]
    public void ApprovePrintCard_client_role_without_evidence_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = EngineeringApproved(clock);
        npi.SendPrintCardToClient(Commercial, clock);

        var act = () => npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, null);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("client-approval-evidence-required");
    }

    [Fact]
    public void ApprovePrintCard_same_role_twice_on_the_same_version_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = EngineeringApproved(clock);

        var act = () => npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-already-approved-by-role");
    }

    [Fact]
    public void PrintCardVersion_is_fully_approved_after_engineering_and_client()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);

        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeFalse();
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeFalse();
        npi.SendPrintCardToClient(Commercial, clock);
        npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, File(npi, clock, AttachmentContext.ClientApproval));
        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeTrue();
    }

    [Fact]
    public void Engineering_can_reject_a_version_and_design_must_make_a_new_one()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);

        npi.RejectPrintCard(Engineer, clock, Calendar, "El código de barras no corresponde", null);

        npi.CurrentPrintCardVersion!.IsRejected.Should().BeTrue();
        npi.CurrentPrintCardVersion.RejectionReason.Should().Be("El código de barras no corresponde");
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.PrintCardChangesRequested);
        npi.DesignTimer.IsRunning.Should().BeTrue("rehacer la versión es trabajo de Diseño");
        npi.EngineeringTimer.IsRunning.Should().BeFalse();
        var aprobar = () => npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        aprobar.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-version-rejected");

        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0002", File(npi, clock), null);
        npi.CurrentPrintCardVersion!.VersionNumber.Should().Be(2);
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.PrintCardInApproval);
    }

    [Fact]
    public void Rejecting_a_printcard_requires_a_reason_and_is_not_allowed_after_approving_it()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);

        var sinMotivo = () => npi.RejectPrintCard(Engineer, clock, Calendar, "  ", null);
        sinMotivo.Should().Throw<NpiValidationException>().Which.Code.Should().Be("printcard-rejection-reason-required");

        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        var yaAprobado = () => npi.RejectPrintCard(Engineer, clock, Calendar, "Me equivoqué", null);
        yaAprobado.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-already-approved-by-role");
    }

    [Fact]
    public void The_client_cannot_ask_for_changes_or_reject_before_the_printcard_is_sent()
    {
        var clock = TestClock.MondayMorning();
        var npi = EngineeringApproved(clock);

        var cambios = () => npi.RequestClientChanges(Commercial, clock, Calendar, null, ["Otro color"]);
        var rechazo = () => npi.RejectByClient(Commercial, clock, Calendar, "No lo quiere", null);

        cambios.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-not-sent-to-client");
        rechazo.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-not-sent-to-client");
    }

    [Fact]
    public void Client_changes_are_a_checklist_that_design_must_complete_before_the_new_version()
    {
        var clock = TestClock.MondayMorning();
        var npi = WithClientChanges(clock, "Logo más grande", "  ", "Cambiar pantone a 485 C");

        var request = ClientRequest(npi);
        request.Checklist.Select(i => i.Text).Should().Equal("Logo más grande", "Cambiar pantone a 485 C");
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.PrintCardChangesRequested);

        npi.SetClientChangeItemDone(Designer, clock, request.Checklist[0].Id, true);
        var incompleto = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0002", File(npi, clock), null);
        incompleto.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("client-changes-pending");

        npi.SetClientChangeItemDone(Designer, clock, ClientRequest(npi).Checklist[1].Id, true);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0002", File(npi, clock), null);

        var atendida = ClientRequest(npi);
        atendida.IsResolved.Should().BeTrue("la nueva versión atiende los cambios del cliente");
        atendida.Checklist.Should().OnlyContain(i => i.IsDone && i.DoneByName == Designer.Name);
    }

    [Fact]
    public void A_checklist_item_can_be_unchecked_and_foreign_items_are_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = WithClientChanges(clock, "Logo más grande");
        var itemId = ClientRequest(npi).Checklist[0].Id;

        npi.SetClientChangeItemDone(Designer, clock, itemId, true);
        npi.SetClientChangeItemDone(Designer, clock, itemId, false);
        ClientRequest(npi).Checklist[0].IsDone.Should().BeFalse();

        var ajeno = () => npi.SetClientChangeItemDone(Designer, clock, Guid.NewGuid(), true);
        ajeno.Should().Throw<NpiValidationException>().Which.Code.Should().Be("client-change-item-not-found");
    }

    [Fact]
    public void Client_changes_accept_the_annotated_pdf_and_screenshots()
    {
        var clock = TestClock.MondayMorning();
        var npi = EngineeringApproved(clock);
        npi.SendPrintCardToClient(Commercial, clock);
        var pdf = npi.RegisterAttachment(Commercial, clock, "PrintCard anotado.pdf", "application/pdf", 100, "k-pdf", null, AttachmentContext.ChangeRequest);
        var shot = npi.RegisterAttachment(Commercial, clock, "correo.png", "image/png", 100, "k-png", null, AttachmentContext.ChangeRequest);

        npi.RequestClientChanges(Commercial, clock, Calendar, null, ["Cambiar el Pantone del logo"], [pdf.Id, shot.Id]);

        ClientRequest(npi).ImageAttachmentIds.Should().Equal(pdf.Id, shot.Id);

        // Las demás solicitudes siguen siendo sólo imágenes.
        var questionWithPdf = () => npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Engineering, "¿Y esto?", [pdf.Id]);
        questionWithPdf.Should().Throw<NpiValidationException>().Which.Code.Should().Be("request-image-not-image");
    }

    [Fact]
    public void Client_changes_need_at_least_one_item()
    {
        var clock = TestClock.MondayMorning();
        var npi = EngineeringApproved(clock);
        npi.SendPrintCardToClient(Commercial, clock);

        var act = () => npi.RequestClientChanges(Commercial, clock, Calendar, "Comentario sin cambios", ["", " "]);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("change-request-empty");
    }

    [Fact]
    public void NewPrintCardVersion_does_not_inherit_approvals_from_the_previous_version_invariant_4()
    {
        var clock = TestClock.MondayMorning();
        var npi = WithClientChanges(clock, "El cliente pide otro color");
        npi.SetClientChangeItemDone(Designer, clock, ClientRequest(npi).Checklist[0].Id, true);

        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0002", File(npi, clock), null);

        npi.PrintCardVersions.Should().HaveCount(2);
        npi.PrintCardVersions.First(v => v.VersionNumber == 1).Approvals.Should().HaveCount(2, "las aprobaciones de una versión anterior quedan históricas, ligadas a su propia versión");
        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeFalse();
        npi.CurrentPrintCardVersion!.Approvals.Should().ContainSingle(a => a.Role == ApproverRole.Design, "sólo la aprobación automática de Diseño");
        npi.PrintCardSentToClientAt.Should().BeNull("la versión nueva necesita su propio envío al cliente");
    }

    [Fact]
    public void RejectByClient_requires_a_mandatory_comment_decision_7()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithFullyApprovedPrintCard(clock);

        var act = () => npi.RejectByClient(Commercial, clock, Calendar, "   ", null);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("client-rejection-comment-required");
    }

    [Fact]
    public void RejectByClient_evidence_is_optional_decision_7()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithFullyApprovedPrintCard(clock);

        npi.RejectByClient(Commercial, clock, Calendar, "El cliente cancela definitivamente", null);

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Rejected);
        npi.TerminalReason.Should().Be(TerminalReason.ClientRejection);
        npi.ClientRejection!.EvidenceAttachmentId.Should().BeNull();
    }

    [Fact]
    public void RejectByClient_closes_all_timers_as_a_terminal_rejection()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithFullyApprovedPrintCard(clock);

        npi.RejectByClient(Commercial, clock, Calendar, "El cliente cancela definitivamente", File(npi, clock, AttachmentContext.ClientRejection));

        npi.GlobalTimer.IsClosed.Should().BeTrue();
        npi.EngineeringTimer.IsClosed.Should().BeTrue();
        npi.CommercialTimer.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void LoadPrintCardIntoSystem_requires_engineering_and_client_approval()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);

        var sinIngenieria = () => npi.LoadPrintCardIntoSystem(Designer, clock, Calendar);
        sinIngenieria.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-engineering-approval-missing");

        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        var sinCliente = () => npi.LoadPrintCardIntoSystem(Designer, clock, Calendar);
        sinCliente.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("client-approval-missing");
    }

    [Fact]
    public void LoadPrintCardIntoSystem_only_closes_design_timer_and_is_not_terminal_decision_3()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithFullyApprovedPrintCard(clock);

        npi.LoadPrintCardIntoSystem(Designer, clock, Calendar);

        npi.DesignTimer.IsClosed.Should().BeTrue();
        npi.LifecycleStatus.Should().Be(LifecycleStatus.Active, "cargar el PrintCard no cierra el ciclo (decisión 3)");
        npi.GlobalTimer.IsClosed.Should().BeFalse();
        npi.EngineeringTimer.IsClosed.Should().BeFalse();
        npi.CommercialTimer.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void Commercial_must_check_the_points_design_left_before_registering_the_client_approval()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null, ["Confirmar el pantone 485 C", "  ", "Revisar el texto legal"]);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, clock);

        var points = npi.CurrentPrintCardVersion!.CommercialChecklist;
        points.Select(p => p.Text).Should().Equal("Confirmar el pantone 485 C", "Revisar el texto legal");

        var approve = () => npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, File(npi, clock, AttachmentContext.ClientApproval));
        approve.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-observations-pending");

        foreach (var p in points) npi.SetPrintCardObservationDone(Commercial, clock, p.Id, true);
        npi.CurrentPrintCardVersion!.CommercialChecklist.Should().OnlyContain(p => p.IsDone && p.DoneByName == Commercial.Name);
        approve.Should().NotThrow();
    }

    [Fact]
    public void A_version_without_points_is_approved_as_before()
    {
        var clock = TestClock.MondayMorning();
        var npi = EngineeringApproved(clock);
        npi.SendPrintCardToClient(Commercial, clock);

        npi.CurrentPrintCardVersion!.CommercialChecklist.Should().BeEmpty();
        var approve = () => npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, File(npi, clock, AttachmentContext.ClientApproval));
        approve.Should().NotThrow();
    }
}
