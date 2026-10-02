using FluentAssertions;
using NpiManagement.Application.Attachments;
using Xunit;

namespace NpiManagement.Application.Tests.Attachments;

public class FileNameSanitizerTests
{
    [Fact]
    public void Keeps_a_normal_file_name_unchanged()
    {
        FileNameSanitizer.Sanitize("printcard-v1.pdf").Should().Be("printcard-v1.pdf");
    }

    [Fact]
    public void Strips_path_traversal_attempts_down_to_the_file_name_only()
    {
        FileNameSanitizer.Sanitize("../../etc/passwd").Should().Be("passwd");
    }

    [Fact]
    public void Strips_windows_style_path_traversal_attempts()
    {
        FileNameSanitizer.Sanitize(@"..\..\Windows\System32\evil.dll").Should().Be("evil.dll");
    }

    [Fact]
    public void Replaces_invalid_file_name_characters()
    {
        var result = FileNameSanitizer.Sanitize("archivo:con*caracteres?raros.pdf");
        result.Should().NotContain(":").And.NotContain("*").And.NotContain("?");
    }

    [Fact]
    public void Falls_back_to_a_default_name_when_nothing_usable_remains()
    {
        FileNameSanitizer.Sanitize("").Should().Be("archivo");
    }

    [Fact]
    public void Truncates_excessively_long_file_names()
    {
        var longName = new string('a', 500) + ".pdf";
        FileNameSanitizer.Sanitize(longName).Length.Should().BeLessThanOrEqualTo(200);
    }
}
