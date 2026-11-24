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

    /// <summary>
    /// 200 MB desde 2026-11-24 (antes 50, y 10 antes del 2026-11-11): Comercial y Diseño suben artes y EPS de
    /// hasta ~180 MB. El endpoint de subida y el web.config de IIS deben permitir al menos esto más el sobre multipart.
    /// </summary>
    public const long DefaultMaxSizeBytes = 200L * 1024 * 1024;

    public long MaxSizeBytes { get; set; } = DefaultMaxSizeBytes;

    public string[] AllowedContentTypes { get; set; } = ["application/pdf", "image/jpeg", "image/png"];
}
