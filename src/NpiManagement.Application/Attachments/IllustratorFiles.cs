using NpiManagement.Domain;

namespace NpiManagement.Application.Attachments;

/// <summary>
/// Archivos .ai de Illustrator (2026-11-12): sólo en artes y PrintCard, donde Diseño y los clientes
/// los usan. Illustrator los guarda por omisión "compatibles con PDF", así que por dentro empiezan con
/// %PDF: se sigue validando la firma real del archivo y el navegador los puede mostrar como PDF.
/// El tipo que manda el navegador para .ai varía (application/postscript, octet-stream o vacío), por
/// eso se decide por la extensión y se guarda siempre con un tipo fijo.
/// </summary>
public static class IllustratorFiles
{
    public const string ContentType = "application/illustrator";

    public static bool Applies(string fileName, AttachmentContext context) =>
        context is AttachmentContext.Artwork or AttachmentContext.PrintCard or AttachmentContext.PrintCardAnnex
        && Path.GetExtension(fileName ?? string.Empty).Equals(".ai", StringComparison.OrdinalIgnoreCase);
}
