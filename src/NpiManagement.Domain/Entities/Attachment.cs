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
    /// <summary>ContentType con el que se registran los links (no hay archivo en IFileStorage).</summary>
    public const string LinkContentType = "text/uri-list";

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

    /// <summary>
    /// Si no es null, el adjunto es un link externo (OneDrive, SharePoint, WeTransfer…) en vez de
    /// un archivo: se usa para artes pesadas que exceden el límite de archivo. FileName guarda el
    /// título y StorageKey una clave sintética única ("link:…"), nunca una ruta de IFileStorage.
    /// </summary>
    public string? Url { get; }

    public bool IsLink => Url is not null;

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
        int version,
        string? url = null)
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
        Url = url;
    }
}
