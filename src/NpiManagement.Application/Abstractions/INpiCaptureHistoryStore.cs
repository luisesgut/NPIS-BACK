using NpiManagement.Domain.Entities;

namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Bitácora de cambios de la captura (campo por campo). Aparte del agregado, como el chat: es sólo
/// historia y no debe cargarse en cada acción. AddRange sólo prepara; confirma IUnitOfWork.
/// </summary>
public interface INpiCaptureHistoryStore
{
    void AddRange(IEnumerable<CaptureChange> changes);

    /// <summary>Todos los cambios del NPI, del más antiguo al más reciente.</summary>
    Task<IReadOnlyList<CaptureChange>> ListAsync(Guid npiId, CancellationToken cancellationToken);
}
