using NpiManagement.Domain;

namespace NpiManagement.Domain.Entities;

/// <summary>
/// Metadatos de un archivo adjunto al NPI. El contenido binario nunca vive aquí ni en la fila
/// del NPI — se guarda a través de IFileStorage (Infrastructure) y sólo se referencia por
/// StorageKey (ver docs/architecture-decisions.md ADR-021). La validación de tipo/tamaño/firma
/// de archivo ocurre en Application/Infrastructure antes de registrar este metadato.
/// </summary>
public sealed class Attachment
{
    public Guid Id { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long SizeBytes { get; }
    public string StorageKey { get; }
    public string? Sha256Hash { get; }
    public Guid UploadedByUserId { get; }
    public DateTimeOffset UploadedAt { get; }
    public AttachmentContext Context { get; }
    public int Version { get; }

    internal Attachment(
        Guid id,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        string? sha256Hash,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAt,
        AttachmentContext context,
        int version)
    {
        Id = id;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        Sha256Hash = sha256Hash;
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
        Context = context;
        Version = version;
    }
}
