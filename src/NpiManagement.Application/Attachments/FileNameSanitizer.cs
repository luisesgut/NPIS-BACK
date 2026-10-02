namespace NpiManagement.Application.Attachments;

/// <summary>
/// Sanea el nombre de archivo provisto por el cliente: quita cualquier componente de ruta
/// (evita path traversal) y caracteres inválidos. El nombre saneado se guarda como metadato,
/// nunca se usa directamente como ruta física (el StorageKey lo genera el servidor — ADR-021).
/// </summary>
public static class FileNameSanitizer
{
    private const int MaxLength = 200;

    public static string Sanitize(string fileName)
    {
        var nameOnly = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(nameOnly)) return "archivo";

        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(nameOnly.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(cleaned)) return "archivo";
        return cleaned.Length > MaxLength ? cleaned[..MaxLength] : cleaned;
    }
}
