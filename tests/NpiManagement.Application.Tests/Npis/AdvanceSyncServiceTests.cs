using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis.Advance;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>Sincronización con Advance, el repositorio de PrintCards de la planta (2026-11-12).</summary>
public class AdvanceSyncServiceTests
{
    private static readonly Actor Commercial = new(Guid.NewGuid(), "UsuarioComercial", "Comercial");
    private static readonly Actor Engineer = new(Guid.NewGuid(), "UsuarioIngenieria", "Ingeniero");
    private static readonly Actor Designer = new(Guid.NewGuid(), "UsuarioDiseno", "Diseñador");
    private static readonly IBusinessCalendar Calendar = new MexicoBusinessCalendar();
    private static readonly byte[] AdvancePdf = "%PDF-1.7 advance v1"u8.ToArray();

    private sealed class FakeAdvance(bool enabled) : IAdvanceClient
    {
        public bool IsEnabled => enabled;
        public DateTimeOffset? AppliesFrom { get; set; }
        public List<AdvanceDocumentDto> Docs { get; } = [new("printcard", "PrintCard", true), new("fichaTecnica", "Especificación técnica", false)];
        public byte[] Pdf { get; set; } = AdvancePdf;
        public int Calls { get; private set; }

        public Task<AdvanceStatusDto?> GetStatusAsync(string printCardCode, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<AdvanceStatusDto?>(new AdvanceStatusDto(printCardCode, true, "DESC", null, Docs.Count(d => d.Subido), Docs.Count, false, Docs, "http://advance/" + printCardCode));
        }

        public Task<AdvanceFile?> DownloadPrintCardAsync(string printCardCode, CancellationToken cancellationToken) =>
            Task.FromResult<AdvanceFile?>(new AdvanceFile(Pdf, printCardCode + ".pdf", "application/pdf"));
    }

    /// <summary>NPI con el PrintCard PC-0001 aprobado por Ingeniería y por el cliente, sin cargar.</summary>
    private static Npi ApprovedByClient(TestClock clock)
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), Commercial, clock, NpiCaptureData.Empty with { Cliente = "DESTINY", NumeroCotizacion = "1001" }, Priority.Media);
        npi.Submit(Commercial, clock, Calendar, NpiNumber.FromSequenceValue(60));
        npi.AssignEngineering(Engineer.UserId, new Actor(Guid.NewGuid(), "AdminIngenieria", "Admin"), clock);
        npi.AssignDesign(Designer.UserId, new Actor(Guid.NewGuid(), "AdminDiseno", "Admin"), clock);
        npi.ApproveDesignFeasibility(Designer, clock, null, ArtClassification.A);
        npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);
        npi.ValidateArtwork(Designer, clock);
        npi.LinkSisproQuote(Commercial, clock, "678", "Cotizada", "{}", []);
        npi.AcceptSisproQuote(Commercial, clock, Calendar, null, null);
        var file = npi.RegisterLink(Designer, clock, "PrintCard", "https://example.com/pc.pdf", AttachmentContext.PrintCard).Id;
        npi.CreatePrintCardVersion(Designer, clock, Calendar, "PC-0001", file, null);
        npi.ApprovePrintCard(Engineer, clock, Calendar, ApproverRole.Engineering, null, null);
        npi.SendPrintCardToClient(Commercial, clock);
        var evidence = npi.RegisterLink(Commercial, clock, "OK cliente", "https://example.com/ok.pdf", AttachmentContext.ClientApproval).Id;
        npi.ApprovePrintCard(Commercial, clock, Calendar, ApproverRole.Client, null, evidence);
        return npi;
    }

    [Fact]
    public async Task Uploaded_in_advance_it_gets_loaded_and_the_final_printcard_is_advances_pdf()
    {
        var clock = TestClock.Default();
        var npi = ApprovedByClient(clock);
        var notifications = new FakeNotificationService();
        var sync = new AdvanceSyncService(new FakeAdvance(true), new FakeFileStorage(), clock, Calendar, notifications);

        var result = await sync.SyncAsync(npi, CancellationToken.None);

        result.Changes.Should().Equal("PrintCard cargado en sistema", "PrintCard final actualizado desde Advance");
        npi.PrintCardLoadedAt.Should().NotBeNull();
        npi.FinalPrintCardVersion!.Code.Should().Be("PC-0001");
        npi.Attachments.Single(a => a.Id == npi.FinalPrintCardVersion.AttachmentId).FileName.Should().Be("PC-0001.pdf");
        notifications.Sent.Should().Contain(n => n.Message.Contains("Advance"));

        // La siguiente vuelta, con el mismo PDF, no hace nada.
        (await sync.SyncAsync(npi, CancellationToken.None)).Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task If_the_pdf_changes_in_advance_the_final_printcard_is_updated_again()
    {
        var clock = TestClock.Default();
        var npi = ApprovedByClient(clock);
        var advance = new FakeAdvance(true);
        var sync = new AdvanceSyncService(advance, new FakeFileStorage(), clock, Calendar, new FakeNotificationService());
        await sync.SyncAsync(npi, CancellationToken.None);
        var before = npi.FinalPrintCardVersion!.VersionNumber;

        advance.Pdf = "%PDF-1.7 advance v2 con la cota corregida"u8.ToArray();
        var result = await sync.SyncAsync(npi, CancellationToken.None);

        result.Changes.Should().Equal("PrintCard final actualizado desde Advance");
        npi.FinalPrintCardVersion!.VersionNumber.Should().Be(before + 1);
    }

    [Fact]
    public async Task Not_approved_by_the_client_yet_nothing_is_marked()
    {
        var clock = TestClock.Default();
        var npi = Npi.CreateDraft(Guid.NewGuid(), Commercial, clock, NpiCaptureData.Empty with { Cliente = "DESTINY" }, Priority.Media);
        var advance = new FakeAdvance(true);

        var result = await new AdvanceSyncService(advance, new FakeFileStorage(), clock, Calendar, new FakeNotificationService()).SyncAsync(npi, CancellationToken.None);

        result.Changes.Should().BeEmpty();
        advance.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Npis_created_before_the_connection_are_left_as_they_are()
    {
        var clock = TestClock.Default();
        var npi = ApprovedByClient(clock);
        var advance = new FakeAdvance(true) { AppliesFrom = npi.CreatedAt.AddDays(1) };
        var notifications = new FakeNotificationService();

        var result = await new AdvanceSyncService(advance, new FakeFileStorage(), clock, Calendar, notifications).SyncAsync(npi, CancellationToken.None);

        result.Applies.Should().BeFalse();
        result.Changes.Should().BeEmpty();
        advance.Calls.Should().Be(0);
        npi.PrintCardLoadedAt.Should().BeNull();
        notifications.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task The_et_is_not_marked_automatically_it_stays_manual()
    {
        var clock = TestClock.Default();
        var npi = ApprovedByClient(clock);
        var advance = new FakeAdvance(true);
        advance.Docs[1] = new AdvanceDocumentDto("fichaTecnica", "Especificación técnica", true);

        var result = await new AdvanceSyncService(advance, new FakeFileStorage(), clock, Calendar, new FakeNotificationService()).SyncAsync(npi, CancellationToken.None);

        result.Changes.Should().NotContain("ET creada");
        npi.EtCreatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Turned_off_it_does_not_ask_advance()
    {
        var clock = TestClock.Default();
        var npi = ApprovedByClient(clock);
        var advance = new FakeAdvance(false);

        var result = await new AdvanceSyncService(advance, new FakeFileStorage(), clock, Calendar, new FakeNotificationService()).SyncAsync(npi, CancellationToken.None);

        result.Enabled.Should().BeFalse();
        advance.Calls.Should().Be(0);
        npi.PrintCardLoadedAt.Should().BeNull();
    }
}
