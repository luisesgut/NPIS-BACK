namespace NpiManagement.Application.Abstractions;

/// <summary>Resultado de guardar un archivo — lo único que Application necesita para registrar el Attachment en el dominio.</summary>
public sealed record StoredFile(string StorageKey, long SizeBytes, string Sha256Hash);

/// <summary>
/// Contrato de almacenamiento de archivos (ver docs/architecture-decisions.md ADR-021). La
/// implementación local (Fase 6) guarda fuera del código fuente, con StorageKey generado por el
/// servidor (nunca a partir del nombre de archivo del cliente, para evitar path traversal) y
/// hash SHA-256 calculado al guardar. Reemplazable por S3/Azure Blob/MinIO sin tocar Application.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string sanitizedFileName, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Borra el archivo (al eliminar un NPI). No falla si ya no existe.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
