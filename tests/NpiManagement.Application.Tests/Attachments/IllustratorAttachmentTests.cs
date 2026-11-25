using FluentAssertions;
using NpiManagement.Application.Attachments;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.Attachments;

/// <summary>Archivos .ai de Illustrator: sólo en artes y PrintCard, y guardados compatibles con PDF (2026-11-12).</summary>
public class IllustratorAttachmentTests
{
    private static readonly byte[] PdfCompatibleAi = "%PDF-1.6 %Illustrator"u8.ToArray();
    private static readonly byte[] PostScriptOnlyAi = "%!PS-Adobe-3.0"u8.ToArray();

    private static async Task<Func<string, byte[], AttachmentContext, Task<AttachmentDto>>> UploaderAsync()
    {
        var repo = new FakeNpiRepository();
        var creatorId = Guid.NewGuid();
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(creatorId, "UsuarioComercial", "Creador"), TestClock.Default(), new NpiCaptureDto().ToDomain(), Priority.Media);
        repo.Add(npi);
        var handler = new RegisterAttachmentCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeFileStorage(), new FakeAttachmentPolicy(), new FakeNotificationService());
        var actor = new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial);
        await Task.CompletedTask;
        // El navegador manda .ai como application/postscript (o vacío): no importa, se decide por la extensión.
        return (name, bytes, context) => handler.HandleAsync(new RegisterAttachmentCommand(
            npi.Id, actor, "Creador", name, "application/postscript", bytes.Length, new MemoryStream(bytes), context), CancellationToken.None);
    }

    [Theory]
    [InlineData(AttachmentContext.Artwork)]
    [InlineData(AttachmentContext.PrintCard)]
    public async Task An_ai_compatible_with_pdf_is_accepted_in_artwork_and_printcard(AttachmentContext context)
    {
        var upload = await UploaderAsync();

        var result = await upload("Arte cliente.AI", PdfCompatibleAi, context);

        result.FileName.Should().Be("Arte cliente.AI");
        result.ContentType.Should().Be(IllustratorFiles.ContentType);
    }

    // 2026-11-25: en cualquier adjunto (chat, solicitudes, evidencias), no sólo en artes y PrintCard.
    [Theory]
    [InlineData(AttachmentContext.Comment)]
    [InlineData(AttachmentContext.InfoRequest)]
    [InlineData(AttachmentContext.ChangeRequest)]
    public async Task An_ai_is_accepted_in_any_attachment(AttachmentContext context)
    {
        var upload = await UploaderAsync();

        var result = await upload("arte.ai", PdfCompatibleAi, context);

        result.ContentType.Should().Be(IllustratorFiles.ContentType);
    }

    [Fact]
    public async Task An_ai_not_saved_compatible_with_pdf_explains_how_to_save_it()
    {
        var upload = await UploaderAsync();

        var act = () => upload("arte.ai", PostScriptOnlyAi, AttachmentContext.Artwork);

        await act.Should().ThrowAsync<RequestValidationException>().Where(e => e.Code == "attachment-ai-not-pdf-compatible");
    }
    // .eps (2026-11-24): en artes, PrintCard y anexos; PostScript de texto o EPS binario para DOS.
    [Theory]
    [InlineData(AttachmentContext.Artwork, false)]
    [InlineData(AttachmentContext.PrintCard, false)]
    [InlineData(AttachmentContext.PrintCardAnnex, true)]
    public async Task An_eps_is_accepted_in_artwork_printcard_and_annexes(AttachmentContext context, bool binaryDosEps)
    {
        var upload = await UploaderAsync();
        var bytes = binaryDosEps ? new byte[] { 0xC5, 0xD0, 0xD3, 0xC6, 0x20, 0, 0, 0 } : "%!PS-Adobe-3.0 EPSF-3.0"u8.ToArray();

        var result = await upload("Logo cliente.EPS", bytes, context);

        result.ContentType.Should().Be(EpsFiles.ContentType);
    }

    [Fact]
    public async Task An_eps_is_accepted_in_any_attachment_and_a_fake_one_is_rejected()
    {
        var upload = await UploaderAsync();

        var inChat = await upload("logo.eps", "%!PS-Adobe-3.0 EPSF-3.0"u8.ToArray(), AttachmentContext.Comment);
        inChat.ContentType.Should().Be(EpsFiles.ContentType);

        var fake = () => upload("logo.eps", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, AttachmentContext.Artwork);
        await fake.Should().ThrowAsync<RequestValidationException>().Where(e => e.Code == "attachment-eps-invalid");
    }
}
