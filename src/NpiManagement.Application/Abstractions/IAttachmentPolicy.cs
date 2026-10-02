namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Límites de tamaño/tipo permitido para adjuntos — configurables (ver ADR-021), leídos desde
/// Infrastructure. Application sólo conoce la interfaz, nunca de dónde vienen los valores.
/// </summary>
public interface IAttachmentPolicy
{
    long MaxSizeBytes { get; }

    bool IsAllowedContentType(string contentType);
}
