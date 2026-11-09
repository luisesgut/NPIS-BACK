using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Editar o retirar una solicitud ya enviada, y una pendiente por área (2026-11-04).</summary>
public class NpiRequestEditTests
{
    [Fact]
    public void Diseno_puede_tener_una_pregunta_a_Comercial_y_otra_a_Ingenieria_a_la_vez()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "Pantone del logo");
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Engineering, "¿Zipper a 3 o 4 cm?");

        npi.RespondCommercialInformation(Commercial, clock, Calendar, "Pantone 286 C");
        npi.DesignTimer.IsRunning.Should().BeFalse("todavía espera a Ingeniería");
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.AwaitingInformation);

        npi.RespondEngineeringInformation(Engineer, clock, Calendar, "A 3 cm");
        npi.DesignTimer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Editar_una_solicitud_de_informacion_cambia_el_texto_y_suma_imagenes()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        var first = npi.RegisterAttachment(Designer, clock, "a.png", "image/png", 1, "k1", null, AttachmentContext.InfoRequest);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "• Pantone", [first.Id]);
        var request = npi.InformationRequests.Single();
        var second = npi.RegisterAttachment(Designer, clock, "b.png", "image/png", 1, "k2", null, AttachmentContext.InfoRequest);

        npi.EditInformationRequest(Designer, clock, request.Id, "• Pantone\n• Arte en AI", [second.Id]);

        request.Text.Should().Be("• Pantone\n• Arte en AI");
        request.ImageAttachmentIds.Should().Equal(first.Id, second.Id);
        request.Status.Should().Be(InformationRequestStatus.Pending);
    }

    [Fact]
    public void Retirar_una_solicitud_de_informacion_regresa_el_reloj_a_Diseno_y_pausa_a_Comercial()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "Pantone");
        var request = npi.InformationRequests.Single();

        npi.WithdrawInformationRequest(Designer, clock, Calendar, request.Id);

        request.Status.Should().Be(InformationRequestStatus.Withdrawn);
        npi.DesignTimer.IsRunning.Should().BeTrue();
        npi.CommercialTimer.IsRunning.Should().BeFalse();
        npi.DesignAreaStatus.Should().NotBe(DesignAreaStatus.AwaitingInformation);

        var again = () => npi.EditInformationRequest(Designer, clock, request.Id, "otra cosa");
        again.Should().Throw<InvalidNpiTransitionException>("ya no está pendiente");
    }

    [Fact]
    public void Editar_una_solicitud_de_cambios_agrega_campos_y_comentarios()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.RequestDesignChanges(Designer, clock, Calendar, "El ancho no cuadra", ["ancho"]);
        var request = npi.ChangeRequests.Last();

        npi.EditChangeRequest(Designer, clock, request.Id, "El ancho y el alto no cuadran", ["ancho", "alto"], new Dictionary<string, string> { ["alto"] = "Debe ser 300" });

        request.FieldKeys.Should().Equal("ancho", "alto");
        request.Description.Should().Be("El ancho y el alto no cuadran");
        request.FieldNotes["alto"].Should().Be("Debe ser 300");
        request.IsResolved.Should().BeFalse();
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.ChangesRequested);
    }

    [Fact]
    public void Retirar_una_solicitud_de_cambios_la_cierra_sin_cambios_y_regresa_los_estatus()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.RequestDesignChanges(Designer, clock, Calendar, "El ancho no cuadra", ["ancho"]);
        var request = npi.ChangeRequests.Last();

        npi.WithdrawChangeRequest(Designer, clock, Calendar, request.Id);

        request.WithdrawnAt.Should().NotBeNull();
        request.IsResolved.Should().BeTrue();
        npi.DesignAreaStatus.Should().NotBe(DesignAreaStatus.ChangesRequested);
        npi.CommercialTimer.IsRunning.Should().BeFalse("a Comercial ya no le toca nada");
        npi.DesignTimer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Retirar_los_cambios_de_Ingenieria_regresa_su_estatus()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        var before = npi.EngineeringAreaStatus;
        npi.RequestEngineeringChanges(Engineer, clock, Calendar, "Calibre", ["piCalibre"]);
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.ChangesRequested);

        npi.WithdrawChangeRequest(Engineer, clock, Calendar, npi.ChangeRequests.Last().Id);

        npi.EngineeringAreaStatus.Should().Be(before);
    }
}
