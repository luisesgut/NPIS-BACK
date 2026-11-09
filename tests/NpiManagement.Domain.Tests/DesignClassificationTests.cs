using FluentAssertions;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Exceptions;
using NpiManagement.Domain.ValueObjects;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class DesignClassificationTests
{
    private static Npi ActiveAssigned(IClock clock, string basePantone)
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), Commercial, clock, NpiCaptureData.Empty with { Cliente = "Cliente Prueba", BasePantone = basePantone }, Priority.Media);
        npi.Submit(Commercial, clock, Calendar, NpiNumber.FromSequenceValue(1));
        npi.AssignEngineering(Engineer.UserId, EngineeringAdmin, clock);
        npi.AssignDesign(Designer.UserId, DesignAdmin, clock);
        return npi;
    }

    [Fact]
    public void Design_feasibility_requires_the_art_classification()
    {
        var clock = TestClock.MondayMorning();
        var npi = ActiveAssigned(clock, "NO");

        var act = () => npi.ApproveDesignFeasibility(Designer, clock, null, null);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("art-classification-required");
        npi.DesignFeasibility.Should().BeNull();
    }

    [Fact]
    public void With_base_pantone_the_lab_values_answer_is_required()
    {
        var clock = TestClock.MondayMorning();
        var npi = ActiveAssigned(clock, "SI");

        var act = () => npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.AA);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("lab-values-required");
    }

    [Fact]
    public void Approving_stores_classification_and_lab_values()
    {
        var clock = TestClock.MondayMorning();
        var npi = ActiveAssigned(clock, "SI");

        npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.B, hasLabValues: true);

        npi.ArtClassification.Should().Be(ArtClassification.B);
        npi.HasLabValues.Should().BeTrue();
    }

    [Fact]
    public void Without_base_pantone_lab_values_are_not_stored()
    {
        var clock = TestClock.MondayMorning();
        var npi = ActiveAssigned(clock, "NO");

        npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.C, hasLabValues: true);

        npi.HasLabValues.Should().BeNull();
    }

    [Fact]
    public void Classification_can_be_corrected_after_approval_but_not_before()
    {
        var clock = TestClock.MondayMorning();
        var npi = ActiveAssigned(clock, "NO");

        var tooEarly = () => npi.UpdateDesignClassification(DesignAdmin, clock, ArtClassification.A, null);
        tooEarly.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("design-feasibility-not-approved");

        npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.C);

        // El diseñador ya no la cambia (2026-11-04): sólo el admin de Diseño.
        var byDesigner = () => npi.UpdateDesignClassification(Designer, clock, ArtClassification.AA, null);
        byDesigner.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("design-classification-admin-only");

        npi.UpdateDesignClassification(DesignAdmin, clock, ArtClassification.AA, null);

        npi.ArtClassification.Should().Be(ArtClassification.AA);
        npi.AuditLog.Should().Contain(e => e.Action == "DesignClassificationUpdated");
    }

    [Fact]
    public void The_admin_can_classify_when_assigning_and_the_designer_approves_with_it()
    {
        var clock = TestClock.MondayMorning();
        var npi = Npi.CreateDraft(Guid.NewGuid(), Commercial, clock, NpiCaptureData.Empty with { Cliente = "Cliente Prueba", BasePantone = "SI" }, Priority.Media);
        npi.Submit(Commercial, clock, Calendar, NpiNumber.FromSequenceValue(1));

        npi.AssignDesign(Designer.UserId, DesignAdmin, clock, ArtClassification.A, hasLabValues: true);
        npi.ArtClassification.Should().Be(ArtClassification.A);
        npi.HasLabValues.Should().BeTrue();

        // Ya clasificado: sólo el admin lo corrige; el diseñador aprueba sin volver a capturarlo y no puede cambiarlo al aprobar.
        npi.UpdateDesignClassification(DesignAdmin, clock, ArtClassification.B, true);
        var changingOnApproval = () => npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.AA);
        changingOnApproval.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("design-classification-admin-only");
        npi.ApproveDesignFeasibility(Designer, clock, null, null);

        npi.DesignFeasibility.Should().NotBeNull();
        npi.ArtClassification.Should().Be(ArtClassification.B);
        npi.HasLabValues.Should().BeTrue();
    }

    [Fact]
    public void Classifying_on_assignment_still_requires_the_lab_answer_with_base_pantone()
    {
        var clock = TestClock.MondayMorning();
        var npi = Npi.CreateDraft(Guid.NewGuid(), Commercial, clock, NpiCaptureData.Empty with { Cliente = "Cliente Prueba", BasePantone = "SI" }, Priority.Media);
        npi.Submit(Commercial, clock, Calendar, NpiNumber.FromSequenceValue(1));

        var act = () => npi.AssignDesign(Designer.UserId, DesignAdmin, clock, ArtClassification.A);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("lab-values-required");
        npi.AssignedDesignUserId.Should().BeNull();
    }

    [Fact]
    public void Reassigning_the_designer_keeps_the_design_progress()
    {
        var clock = TestClock.MondayMorning();
        var npi = ActiveAssigned(clock, "NO");
        npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.C);

        npi.AssignDesign(Guid.NewGuid(), DesignAdmin, clock);

        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.FeasibilityApproved);
        npi.ArtClassification.Should().Be(ArtClassification.C);
        npi.AuditLog.Should().Contain(e => e.Action == "DesignReassigned");
    }

    [Fact]
    public void Reassigning_the_engineer_keeps_the_engineering_progress()
    {
        var clock = TestClock.MondayMorning();
        var npi = ActiveAssigned(clock, "NO");
        npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);

        npi.AssignEngineering(Guid.NewGuid(), EngineeringAdmin, clock);

        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.FeasibilityApproved);
        npi.AuditLog.Should().Contain(e => e.Action == "EngineeringReassigned");
    }
}
