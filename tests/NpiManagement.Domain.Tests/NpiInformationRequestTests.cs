using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class NpiInformationRequestTests
{
    [Fact]
    public void RequestInformation_to_client_pauses_design_but_leaves_commercial_untouched()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock); // DesignTimer corriendo (precio ya aceptado)

        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Client, "Necesitamos el color Pantone exacto");

        npi.DesignTimer.IsRunning.Should().BeFalse();
        npi.CommercialTimer.IsRunning.Should().BeFalse("una solicitud al Cliente no involucra a Comercial");
    }

    [Fact]
    public void RequestInformation_to_commercial_pauses_design_and_starts_commercial()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);
        npi.ValidateArtwork(Designer, clock);

        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "¿Cuál es el contacto de compras?");

        npi.DesignTimer.IsRunning.Should().BeFalse();
        npi.CommercialTimer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void RequestInformation_rejects_a_second_pending_request_of_the_same_type()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Client, "Pregunta 1");

        var act = () => npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Client, "Pregunta 2");

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("information-request-already-pending");
    }

    [Fact]
    public void ResolveClientInformation_resumes_design_timer()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Client, "Pregunta");

        npi.ResolveClientInformation(Designer, clock);

        npi.DesignTimer.IsRunning.Should().BeTrue();
        npi.InformationRequests.Single().Status.Should().Be(InformationRequestStatus.Resolved);
    }

    [Fact]
    public void ResolveClientInformation_without_a_pending_request_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        var act = () => npi.ResolveClientInformation(Designer, clock);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("no-pending-client-information-request");
    }

    [Fact]
    public void RespondCommercialInformation_requires_response_text()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "Pregunta");

        var act = () => npi.RespondCommercialInformation(Commercial, clock, Calendar, "  ");

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("information-response-required");
    }

    [Fact]
    public void RespondCommercialInformation_pauses_commercial_and_resumes_design()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "Pregunta");

        npi.RespondCommercialInformation(Commercial, clock, Calendar, "Aquí está la respuesta");

        npi.CommercialTimer.IsRunning.Should().BeFalse();
        npi.DesignTimer.IsRunning.Should().BeTrue();
        npi.InformationRequests.Single().ResponseText.Should().Be("Aquí está la respuesta");
    }

    [Fact]
    public void Questions_to_engineering_pause_design_and_run_engineering_until_answered()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock); // Ingeniería ya dio factibilidad: su reloj está en pausa
        npi.EngineeringTimer.IsRunning.Should().BeFalse();

        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Engineering, "¿El zipper va a 3 cm o a 4 cm del borde?");

        npi.DesignTimer.IsRunning.Should().BeFalse("la duda no le corresponde a Diseño");
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.CommercialTimer.IsRunning.Should().BeFalse();
        npi.DesignAreaStatus.Should().Be(DesignAreaStatus.AwaitingInformation);

        npi.RespondEngineeringInformation(Engineer, clock, Calendar, "A 3 cm");

        npi.EngineeringTimer.IsRunning.Should().BeFalse();
        npi.DesignTimer.IsRunning.Should().BeTrue();
        npi.InformationRequests.Single().ResponseText.Should().Be("A 3 cm");
    }

    [Fact]
    public void RespondEngineeringInformation_requires_a_pending_question_and_text()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);

        var noQuestion = () => npi.RespondEngineeringInformation(Engineer, clock, Calendar, "Respuesta");
        noQuestion.Should().Throw<InvalidNpiTransitionException>();

        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Engineering, "Pregunta");
        var empty = () => npi.RespondEngineeringInformation(Engineer, clock, Calendar, "  ");
        empty.Should().Throw<NpiValidationException>();
    }
}
