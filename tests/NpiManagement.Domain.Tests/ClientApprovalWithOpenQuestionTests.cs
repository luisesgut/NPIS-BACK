using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>
/// NPI-0049 (2026-11-25): Comercial registró la aprobación del cliente mientras Diseño tenía una pregunta al
/// cliente sin contestar ("el texto se sale de la caja"). Primero la respuesta, luego la aprobación.
/// </summary>
public class ClientApprovalWithOpenQuestionTests
{
    private static Npi SentToClient(TestClock clock)
    {
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, clock);
        return npi;
    }

    [Fact]
    public void Con_una_pregunta_de_Diseño_al_cliente_sin_contestar_no_se_registra_la_aprobación_del_cliente()
    {
        var clock = TestClock.MondayMorning();
        var npi = SentToClient(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Client, "El texto abajo de la tabla nutrimental se sale de la caja");

        var approve = () => npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, File(npi, clock, AttachmentContext.ClientApproval));

        approve.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("client-information-pending");
        npi.CurrentPrintCardVersion!.Approvals.Should().NotContain(a => a.Role == ApproverRole.Client);
    }

    [Fact]
    public void Ya_entregada_la_respuesta_a_Diseño_sí_se_registra()
    {
        var clock = TestClock.MondayMorning();
        var npi = SentToClient(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Client, "¿El texto puede ir más chico?");
        npi.ResolveClientInformation(Commercial, clock);

        npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, File(npi, clock, AttachmentContext.ClientApproval));

        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeTrue();
    }

    [Fact]
    public void Una_pregunta_de_Diseño_a_Comercial_no_bloquea_la_aprobación_del_cliente()
    {
        var clock = TestClock.MondayMorning();
        var npi = SentToClient(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "¿Quién es el contacto de compras?");

        npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, File(npi, clock, AttachmentContext.ClientApproval));

        npi.CurrentPrintCardVersion!.IsFullyApproved.Should().BeTrue();
    }
}
