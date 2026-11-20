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

    [Fact]
    public async Task An_ai_is_not_accepted_elsewhere()
    {
        var upload = await UploaderAsync();

        var act = () => upload("arte.ai", PdfCompatibleAi, AttachmentContext.Comment);

        await act.Should().ThrowAsync<RequestValidationException>().Where(e => e.Code == "attachment-content-type-not-allowed");
    }

    [Fact]
    public async Task An_ai_not_saved_compatible_with_pdf_explains_how_to_save_it()
    {
        var upload = await UploaderAsync();

        var act = () => upload("arte.ai", PostScriptOnlyAi, AttachmentContext.Artwork);

        await act.Should().ThrowAsync<RequestValidationException>().Where(e => e.Code == "attachment-ai-not-pdf-compatible");
    }
}
