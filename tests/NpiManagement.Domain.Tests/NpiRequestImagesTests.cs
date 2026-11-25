using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Capturas de pantalla o fotos en las solicitudes de Diseño (2026-11-04).</summary>
public class NpiRequestImagesTests
{
    [Fact]
    public void RequestInformation_guarda_las_imagenes_adjuntas()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        var shot = npi.RegisterAttachment(Designer, clock, "captura.png", "image/png", 100, "k1", null, AttachmentContext.InfoRequest);
        var photo = npi.RegisterAttachment(Designer, clock, "foto.jpg", "image/jpeg", 100, "k2", null, AttachmentContext.InfoRequest);

        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Engineering, "¿Así va el zipper?", [shot.Id, photo.Id, shot.Id]);

        npi.InformationRequests.Single().ImageAttachmentIds.Should().Equal(shot.Id, photo.Id);
    }

    [Fact]
    public void Sin_imagenes_queda_vacio()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "¿Contacto de compras?");

        npi.InformationRequests.Single().ImageAttachmentIds.Should().BeEmpty();
    }

    [Fact]
    public void Rechaza_un_link_o_un_adjunto_de_otro_npi()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        // Desde 2026-11-25 un PDF, .ai o .eps sí se puede; un link no (va en el texto o en Adjuntos).
        var link = npi.RegisterLink(Designer, clock, "Arte", "https://bioflex.sharepoint.com/arte", AttachmentContext.InfoRequest);

        var withLink = () => npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "Pregunta", [link.Id]);
        withLink.Should().Throw<NpiValidationException>();

        var foreign = () => npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "Pregunta", [Guid.NewGuid()]);
        foreign.Should().Throw<NpiValidationException>();

        npi.InformationRequests.Should().BeEmpty("si una imagen no sirve, no se crea la solicitud");
    }

    [Fact]
    public void RequestDesignChanges_guarda_las_imagenes()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        var shot = npi.RegisterAttachment(Designer, clock, "captura.png", "image/png", 100, "k1", null, AttachmentContext.ChangeRequest);

        npi.RequestDesignChanges(Designer, clock, Calendar, "El ancho no coincide con el arte", ["ancho"], null, [shot.Id]);

        npi.ChangeRequests.Last().ImageAttachmentIds.Should().Equal(shot.Id);
    }
}
