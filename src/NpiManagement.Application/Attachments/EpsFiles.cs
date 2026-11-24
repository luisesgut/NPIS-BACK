using NpiManagement.Domain;

namespace NpiManagement.Application.Attachments;

/// <summary>
/// Archivos .eps (2026-11-24, pedido de Diseño): en los mismos lugares que el .ai (artes, PrintCard y sus
/// anexos). A diferencia del .ai, un EPS no es PDF por dentro: el navegador no lo puede mostrar, sólo se
/// descarga. Se valida su firma real (PostScript "%!PS" o el encabezado binario de EPS para DOS) y, como
/// el navegador lo manda con tipos distintos (application/postscript, image/x-eps, octet-stream o vacío),
/// se decide por la extensión y se guarda siempre con un tipo fijo.
/// </summary>
public static class EpsFiles
{
    public const string ContentType = "application/postscript";

    public static bool Applies(string fileName, AttachmentContext context) =>
        context is AttachmentContext.Artwork or AttachmentContext.PrintCard or AttachmentContext.PrintCardAnnex
        && Path.GetExtension(fileName ?? string.Empty).Equals(".eps", StringComparison.OrdinalIgnoreCase);
}
