namespace NpiManagement.Infrastructure.FileStorage;

/// <summary>Bindeado desde configuración ("FileStorage" section) — ver .env.example.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Directorio raíz de almacenamiento local, fuera del código fuente. Por defecto, una
    /// carpeta bajo el directorio temporal del usuario — configurable para apuntar a un disco
    /// dedicado en despliegues reales.
    /// </summary>
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "NpiManagement", "attachments");

    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    public string[] AllowedContentTypes { get; set; } = ["application/pdf", "image/jpeg", "image/png"];
}
