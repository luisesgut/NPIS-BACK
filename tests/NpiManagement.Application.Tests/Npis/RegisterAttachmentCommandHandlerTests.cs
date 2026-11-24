using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public class RegisterAttachmentCommandHandlerTests
{
    private static readonly byte[] ValidPdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x25, 0x25, 0x45, 0x4F, 0x46];

    private static async Task<(FakeNpiRepository Repo, Guid NpiId, Guid CreatorId)> SeedNpiAsync()
    {
        var repo = new FakeNpiRepository();
        var creatorId = Guid.NewGuid();
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(creatorId, "UsuarioComercial", "Creador"), TestClock.Default(), new NpiCaptureDto().ToDomain(), Priority.Media);
        repo.Add(npi);
        return (repo, npi.Id, creatorId);
    }

    [Fact]
    public async Task Registers_a_valid_pdf_and_returns_its_metadata()
    {
        var (repo, npiId, creatorId) = await SeedNpiAsync();
        var handler = new RegisterAttachmentCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeFileStorage(), new FakeAttachmentPolicy(), new FakeNotificationService());
        var actor = new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial);

        var result = await handler.HandleAsync(new RegisterAttachmentCommand(
            npiId, actor, "Creador", "printcard.pdf", "application/pdf", ValidPdfBytes.Length, new MemoryStream(ValidPdfBytes), AttachmentContext.PrintCard),
            CancellationToken.None);

        result.FileName.Should().Be("printcard.pdf");
        result.Context.Should().Be("PrintCard");
        result.Version.Should().Be(1);

        var reloaded = await repo.GetByIdAsync(npiId, CancellationToken.None);
        reloaded!.Attachments.Should().ContainSingle();
    }

    [Fact]
    public async Task Rejects_a_disallowed_content_type()
    {
        var (repo, npiId, creatorId) = await SeedNpiAsync();
        var handler = new RegisterAttachmentCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeFileStorage(), new FakeAttachmentPolicy(), new FakeNotificationService());
        var actor = new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial);

        var act = async () => await handler.HandleAsync(new RegisterAttachmentCommand(
            npiId, actor, "Creador", "virus.exe", "application/x-msdownload", 10, new MemoryStream([1, 2, 3]), AttachmentContext.Comment),
            CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>().Where(e => e.Code == "attachment-content-type-not-allowed");
    }

    [Fact]
    public async Task Rejects_a_file_larger_than_the_policy_limit()
    {
        var (repo, npiId, creatorId) = await SeedNpiAsync();
        var policy = new FakeAttachmentPolicy { MaxSizeBytes = 5 };
        var handler = new RegisterAttachmentCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeFileStorage(), policy, new FakeNotificationService());
        var actor = new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial);

        var act = async () => await handler.HandleAsync(new RegisterAttachmentCommand(
            npiId, actor, "Creador", "grande.pdf", "application/pdf", ValidPdfBytes.Length, new MemoryStream(ValidPdfBytes), AttachmentContext.Comment),
            CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>().Where(e => e.Code == "attachment-too-large");
    }

    [Fact]
    public async Task Rejects_content_whose_signature_does_not_match_the_declared_type()
    {
        var (repo, npiId, creatorId) = await SeedNpiAsync();
        var handler = new RegisterAttachmentCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeFileStorage(), new FakeAttachmentPolicy(), new FakeNotificationService());
        var actor = new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial);
        var fakeJpegBytes = new byte[] { 0x00, 0x00, 0x00, 0x00 }; // no es realmente un JPEG

        var act = async () => await handler.HandleAsync(new RegisterAttachmentCommand(
            npiId, actor, "Creador", "foto.jpg", "image/jpeg", fakeJpegBytes.Length, new MemoryStream(fakeJpegBytes), AttachmentContext.Comment),
            CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>().Where(e => e.Code == "attachment-signature-mismatch");
    }

    [Fact]
    public async Task An_unrelated_user_cannot_attach_to_someone_elses_npi()
    {
        var (repo, npiId, _) = await SeedNpiAsync();
        var handler = new RegisterAttachmentCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeFileStorage(), new FakeAttachmentPolicy(), new FakeNotificationService());
        var stranger = new CurrentUser(Guid.NewGuid(), "UsuarioComercial", AreaType.Comercial);

        var act = async () => await handler.HandleAsync(new RegisterAttachmentCommand(
            npiId, stranger, "Extraño", "nota.pdf", "application/pdf", ValidPdfBytes.Length, new MemoryStream(ValidPdfBytes), AttachmentContext.Comment),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
    // 2026-11-24: ya no se copia el archivo a memoria. Un stream que no se puede rebobinar se pasa a un temporal en disco.
    [Fact]
    public async Task A_stream_that_cannot_rewind_is_still_validated_and_saved_complete()
    {
        var (repo, npiId, creatorId) = await SeedNpiAsync();
        var handler = new RegisterAttachmentCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeFileStorage(), new FakeAttachmentPolicy(), new FakeNotificationService());
        var actor = new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial);

        var result = await handler.HandleAsync(new RegisterAttachmentCommand(
            npiId, actor, "Creador", "arte.pdf", "application/pdf", ValidPdfBytes.Length, new ForwardOnlyStream(ValidPdfBytes), AttachmentContext.Artwork),
            CancellationToken.None);

        result.SizeBytes.Should().Be(ValidPdfBytes.Length);
    }

    private sealed class ForwardOnlyStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
