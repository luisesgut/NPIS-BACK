namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Borrado definitivo de un NPI (2026-11-03, sólo AdminTotal): el NPI y todo lo que cuelga de él
/// (bitácora, adjuntos, versiones, comentarios...), sus notificaciones y sus archivos. Como el NPI
/// desaparece con su bitácora, la implementación deja constancia en el log del servidor.
/// </summary>
public interface INpiDeletion
{
    Task DeleteAsync(Guid npiId, string npiLabel, Guid actorUserId, string actorName, CancellationToken cancellationToken);
}
