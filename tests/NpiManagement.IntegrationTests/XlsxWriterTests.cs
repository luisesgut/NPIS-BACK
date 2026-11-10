using System.IO.Compression;
using System.Xml.Linq;
using FluentAssertions;
using NpiManagement.Infrastructure.Export;
using Xunit;

namespace NpiManagement.IntegrationTests;

public sealed class XlsxWriterTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void Column_letters_follow_excel(int index, string expected) =>
        XlsxWriter.Column(index).Should().Be(expected);

    [Fact]
    public void Builds_a_valid_workbook_with_header_and_escaped_text()
    {
        var bytes = XlsxWriter.Build("Mis reportes", ["Fecha", "Descripción"],
            [["10/11/2026 11:00", "No guarda <la> bolsa & \"zipper\"\nsegunda línea"], ["10/11/2026 12:00", null]]);

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).Should().Contain(["[Content_Types].xml", "xl/workbook.xml", "xl/worksheets/sheet1.xml", "xl/styles.xml"]);

        using var sheetStream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var sheet = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = sheet.Descendants(ns + "row").ToList();
        rows.Should().HaveCount(3);
        var texts = sheet.Descendants(ns + "t").Select(t => t.Value).ToList();
        texts.Should().ContainInOrder("Fecha", "Descripción", "10/11/2026 11:00", "No guarda <la> bolsa & \"zipper\"\nsegunda línea");
        sheet.Descendants(ns + "autoFilter").Single().Attribute("ref")!.Value.Should().Be("A1:B3");

        using var workbookStream = zip.GetEntry("xl/workbook.xml")!.Open();
        XDocument.Load(workbookStream).Descendants(ns + "sheet").Single().Attribute("name")!.Value.Should().Be("Mis reportes");
    }
}
