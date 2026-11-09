using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Reemplazar el archivo del PrintCard vigente antes de que Ingeniería lo revise (2026-11-09).</summary>
public class NpiPrintCardReplaceFileTests
{
    [Fact]
    public void Antes_de_la_revisión_de_Ingeniería_se_reemplaza_el_archivo_en_la_misma_versión()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);
        var correcto = npi.RegisterAttachment(Designer, clock, "PC-0001 bueno.pdf", "application/pdf", 10, "k-bueno", null, AttachmentContext.PrintCard);

        npi.ReplacePrintCardFile(Designer, clock, correcto.Id);

        npi.PrintCardVersions.Should().ContainSingle();
        npi.CurrentPrintCardVersion!.AttachmentId.Should().Be(correcto.Id);
        npi.AuditLog.Should().Contain(e => e.Action == "PrintCardFileReplaced");
    }

    [Fact]
    public void Ya_revisada_por_Ingeniería_no_se_reemplaza()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        var otro = npi.RegisterAttachment(Designer, clock, "otro.pdf", "application/pdf", 10, "k-otro", null, AttachmentContext.PrintCard);

        var act = () => npi.ReplacePrintCardFile(Designer, clock, otro.Id);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-already-reviewed");
    }
}
