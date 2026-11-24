using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Anexos del PrintCard (2026-11-24): acompañan al archivo principal; lo que se aprueba sigue siendo el principal.</summary>
public class NpiPrintCardAnnexTests
{
    private static Entities.Attachment Annex(Npi npi, TestClock clock, string name) =>
        npi.RegisterAttachment(Designer, clock, name, "application/pdf", 10, $"k-{name}", null, AttachmentContext.PrintCardAnnex);

    [Fact]
    public void La_versión_se_genera_con_su_archivo_principal_y_sus_anexos()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        var main = File(npi, clock);
        var editable = Annex(npi, clock, "PC-0001.ai");
        var reverso = Annex(npi, clock, "reverso.pdf");

        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", main, null, annexAttachmentIds: [editable.Id, reverso.Id, editable.Id, main]);

        var version = npi.CurrentPrintCardVersion!;
        version.AttachmentId.Should().Be(main);
        version.AnnexAttachmentIds.Should().Equal(editable.Id, reverso.Id);
    }

    [Fact]
    public void Sólo_cuentan_como_anexo_los_archivos_subidos_como_anexo_y_hay_un_máximo()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        var suelto = npi.RegisterAttachment(Designer, clock, "otro.pdf", "application/pdf", 10, "k-otro", null, AttachmentContext.Comment);

        var wrong = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null, annexAttachmentIds: [suelto.Id]);
        wrong.Should().Throw<NpiValidationException>().Which.Code.Should().Be("printcard-annex-wrong-context");

        var many = Enumerable.Range(1, Npi.MaxPrintCardAnnexes + 1).Select(i => Annex(npi, clock, $"a{i}.pdf").Id).ToList();
        var tooMany = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null, annexAttachmentIds: many);
        tooMany.Should().Throw<NpiValidationException>().Which.Code.Should().Be("too-many-printcard-annexes");
    }

    [Fact]
    public void Se_agregan_anexos_a_la_versión_vigente_aunque_Ingeniería_ya_la_haya_aprobado()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        var editable = Annex(npi, clock, "PC-0001.ai");

        npi.AddPrintCardAnnexes(Designer, clock, [editable.Id]);

        npi.CurrentPrintCardVersion!.AnnexAttachmentIds.Should().Equal(editable.Id);
        npi.CurrentPrintCardVersion.Approvals.Should().Contain(a => a.Role == ApproverRole.Engineering, "anexar no regresa la revisión");
        npi.AuditLog.Should().Contain(e => e.Action == "PrintCardAnnexesAdded" && e.Details!.Contains("PC-0001.ai"));
    }

    [Fact]
    public void A_una_versión_rechazada_no_se_le_agregan_anexos_y_un_anexo_no_pasa_a_otra_versión()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        var editable = Annex(npi, clock, "PC-0001.ai");
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null, annexAttachmentIds: [editable.Id]);
        npi.RejectPrintCard(Engineer, clock, Calendar, "Falta el reverso", null);

        var add = () => npi.AddPrintCardAnnexes(Designer, clock, [Annex(npi, clock, "x.pdf").Id]);
        add.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("printcard-version-rejected");

        var reuse = () => npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0002", File(npi, clock), null, annexAttachmentIds: [editable.Id]);
        reuse.Should().Throw<NpiValidationException>().Which.Code.Should().Be("printcard-annex-in-use");
    }

    [Fact]
    public void Quien_subió_un_anexo_lo_puede_eliminar_y_se_quita_de_la_versión()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateReadyForPrintCard(clock);
        var equivocado = Annex(npi, clock, "equivocado.pdf");
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", File(npi, clock), null, annexAttachmentIds: [equivocado.Id]);

        npi.RemoveAttachment(Designer, clock, equivocado.Id);

        npi.CurrentPrintCardVersion!.AnnexAttachmentIds.Should().BeEmpty();
        npi.Attachments.Should().NotContain(a => a.Id == equivocado.Id);
    }
}
