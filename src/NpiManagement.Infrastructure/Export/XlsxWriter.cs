using System.IO.Compression;
using System.Security;
using System.Text;

namespace NpiManagement.Infrastructure.Export;

/// <summary>
/// Genera un .xlsx de una sola hoja (encabezado en negritas, filtro y fila fija) sin librerías
/// externas: un xlsx es un zip con unos cuantos XML (Office Open XML). Para listados sencillos
/// basta; si algún día se necesitan fórmulas o formato fino, cambiar a una librería.
/// Todos los valores van como texto (inline strings), así Excel no "corrige" folios ni fechas.
/// </summary>
public static class XlsxWriter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Build(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows, IReadOnlyList<double>? columnWidths = null)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                <Default Extension="xml" ContentType="application/xml"/>
                <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            Add(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Add(zip, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                <sheets><sheet name="{Escape(SafeSheetName(sheetName))}" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            Add(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            // Estilo 1: encabezado en negritas con fondo; estilo 2: texto con ajuste de línea arriba.
            Add(zip, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font></fonts>
                <fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF153E3E"/><bgColor indexed="64"/></patternFill></fill></fills>
                <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                <cellXfs count="3">
                <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
                <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>
                </cellXfs>
                </styleSheet>
                """);
            Add(zip, "xl/worksheets/sheet1.xml", Sheet(headers, rows, columnWidths));
        }
        return buffer.ToArray();
    }

    private static string Sheet(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows, IReadOnlyList<double>? widths)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        // Encabezado fijo al bajar.
        sb.Append("""<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");
        sb.Append("<cols>");
        for (var c = 0; c < headers.Count; c++)
        {
            var width = widths is not null && c < widths.Count ? widths[c] : 18;
            sb.Append($"""<col min="{c + 1}" max="{c + 1}" width="{width.ToString(System.Globalization.CultureInfo.InvariantCulture)}" customWidth="1"/>""");
        }
        sb.Append("</cols><sheetData>");

        var rowNumber = 1;
        AppendRow(sb, rowNumber, headers, style: 1);
        foreach (var row in rows)
            AppendRow(sb, ++rowNumber, row, style: 2);

        sb.Append("</sheetData>");
        sb.Append($"""<autoFilter ref="A1:{Column(headers.Count - 1)}{rowNumber}"/>""");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int rowNumber, IReadOnlyList<string?> cells, int style)
    {
        sb.Append($"""<row r="{rowNumber}">""");
        for (var c = 0; c < cells.Count; c++)
        {
            var value = cells[c];
            if (string.IsNullOrEmpty(value)) continue;
            sb.Append($"""<c r="{Column(c)}{rowNumber}" t="inlineStr" s="{style}"><is><t xml:space="preserve">{Escape(Clean(value))}</t></is></c>""");
        }
        sb.Append("</row>");
    }

    /// <summary>Letra(s) de columna: 0 → A, 25 → Z, 26 → AA.</summary>
    internal static string Column(int index)
    {
        var name = string.Empty;
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
            name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    // Excel tiene un límite de 32,767 caracteres por celda y no acepta caracteres de control.
    private static string Clean(string value)
    {
        var text = new string(value.Where(ch => ch is '\n' or '\t' || !char.IsControl(ch)).ToArray()).Replace("\r", string.Empty);
        return text.Length > 32_000 ? text[..32_000] : text;
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "Hoja1";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content.Trim());
        stream.Write(bytes);
    }
}
