using FluentAssertions;
using NpiManagement.Application.Attachments;
using Xunit;

namespace NpiManagement.Application.Tests.Attachments;

public class FileSignatureValidatorTests
{
    [Fact]
    public void Recognizes_a_valid_pdf_header()
    {
        byte[] header = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34]; // "%PDF-1.4"
        FileSignatureValidator.MatchesContentType(header, "application/pdf").Should().BeTrue();
    }

    [Fact]
    public void Recognizes_a_valid_jpeg_header()
    {
        byte[] header = [0xFF, 0xD8, 0xFF, 0xE0];
        FileSignatureValidator.MatchesContentType(header, "image/jpeg").Should().BeTrue();
    }

    [Fact]
    public void Recognizes_a_valid_png_header()
    {
        byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        FileSignatureValidator.MatchesContentType(header, "image/png").Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_pdf_header_declared_as_png()
    {
        byte[] pdfHeader = [0x25, 0x50, 0x44, 0x46];
        FileSignatureValidator.MatchesContentType(pdfHeader, "image/png").Should().BeFalse();
    }

    [Fact]
    public void Rejects_an_executable_disguised_with_a_pdf_content_type()
    {
        byte[] exeHeader = [0x4D, 0x5A]; // "MZ" — cabecera real de un ejecutable Windows
        FileSignatureValidator.MatchesContentType(exeHeader, "application/pdf").Should().BeFalse();
    }

    [Fact]
    public void Rejects_an_unsupported_content_type_outright()
    {
        byte[] header = [0x25, 0x50, 0x44, 0x46];
        FileSignatureValidator.MatchesContentType(header, "application/x-msdownload").Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_header_shorter_than_the_signature()
    {
        byte[] tooShort = [0x25, 0x50];
        FileSignatureValidator.MatchesContentType(tooShort, "application/pdf").Should().BeFalse();
    }
}
