using FluentAssertions;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Entities;
using NpiManagement.Domain.ValueObjects;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>El admin de Diseño revisa el arte antes de asignar, sus indicaciones y eliminar adjuntos (2026-11-09).</summary>
public class NpiDesignAdminReviewTests
{
    private static Npi SubmittedWithoutDesigner(IClock clock)
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), Commercial, clock, NpiCaptureData.Empty with { Cliente = "Cliente Prueba" }, Priority.Media);
        npi.Submit(Commercial, clock, Calendar, NpiNumber.FromSequenceValue(1));
        return npi;
    }

    [Fact]
    public void El_admin_valida_las_artes_antes_de_asignar_y_el_diseñador_las_recibe_validadas()
    {
        var clock = TestClock.MondayMorning();
        var npi = SubmittedWithoutDesigner(clock);

        npi.ValidateArtwork(DesignAdmin, clock);
        npi.AssignDesign(Designer.UserId, DesignAdmin, clock, ArtClassification.A, false);

        npi.ArtesValidadasAt.Should().NotBeNull();
        npi.AssignedDesignUserId.Should().Be(Designer.UserId);
    }

    [Fact]
    public void El_admin_rechaza_artes_y_pide_cambios_sin_diseñador_pero_un_diseñador_no()
    {
        var clock = TestClock.MondayMorning();
        var npi = SubmittedWithoutDesigner(clock);

        npi.RejectArtwork(DesignAdmin, clock, Calendar, "Falta el arte en AI", null);
        npi.ChangeRequests.Should().ContainSingle(c => c.Origin == ChangeRequestOrigin.ArtworkRejected);

        var other = SubmittedWithoutDesigner(clock);
        var byDesigner = () => other.ValidateArtwork(Designer, clock);
        byDesigner.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("design-not-assigned");
    }

    [Fact]
    public void Indicaciones_para_el_diseñador_sólo_del_admin()
    {
        var clock = TestClock.MondayMorning();
        var npi = SubmittedWithoutDesigner(clock);

        npi.SetDesignInstructions(DesignAdmin, clock, "  Usar el logo del NPI-0021  ");
        npi.DesignInstructions.Should().Be("Usar el logo del NPI-0021");

        var byDesigner = () => npi.SetDesignInstructions(Designer, clock, "otra cosa");
        byDesigner.Should().Throw<InvalidNpiTransitionException>();

        npi.SetDesignInstructions(DesignAdmin, clock, "");
        npi.DesignInstructions.Should().BeNull();
    }

    [Fact]
    public void Eliminar_un_adjunto_suelto_sí_pero_no_la_evidencia_ni_el_de_otro()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        var suelto = npi.RegisterAttachment(Commercial, clock, "equivocado.pdf", "application/pdf", 10, "k1", null, AttachmentContext.Artwork);
        var foto = npi.RegisterAttachment(Designer, clock, "foto.png", "image/png", 10, "k2", null, AttachmentContext.InfoRequest);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "Pantone", [foto.Id]);

        var ajeno = () => npi.RemoveAttachment(Engineer, clock, suelto.Id);
        ajeno.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("attachment-not-yours");

        var evidencia = () => npi.RemoveAttachment(Designer, clock, foto.Id);
        evidencia.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("attachment-in-use");

        npi.RemoveAttachment(Commercial, clock, suelto.Id);
        npi.Attachments.Should().NotContain(a => a.Id == suelto.Id);
    }
}
