using NpiManagement.Domain;

namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Repositorio específico del agregado Npi — no genérico (instrucción explícita del pedido).
/// Ningún otro agregado/entidad de este sistema necesita repositorio propio: Quotation y los
/// catálogos se consultan directamente (son de sólo lectura desde el punto de vista de los
/// casos de uso de NPI); las entidades satélite (comentarios, adjuntos, etc.) sólo se acceden
/// a través del agregado Npi ya cargado. El "Unit of Work" es el propio DbContext de
/// Infrastructure — SaveChangesAsync confirma la transacción, sin una clase UoW adicional.
/// </summary>
public interface INpiRepository
{
    Task<Npi?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Npi?> GetByNpiNumberAsync(string npiNumber, CancellationToken cancellationToken);

    void Add(Npi npi);

    /// <summary>
    /// Token opaco de concurrencia optimista del Npi ya cargado (respaldado por el rowversion de
    /// SQL Server en Infrastructure — Application nunca interpreta su contenido, sólo lo devuelve
    /// al cliente y lo compara). Ver docs/api-contract.md §0.
    /// </summary>
    byte[] GetRowVersion(Npi npi);

    /// <summary>
    /// Declara qué versión creía tener el cliente al enviar su solicitud, para que el guardado
    /// falle con conflicto de concurrencia si alguien más ya cambió el registro desde entonces —
    /// no sólo si cambia dentro de esta misma request (ver Fase 8: corrección de RowVersion).
    /// </summary>
    void SetExpectedRowVersion(Npi npi, byte[] expectedRowVersion);
}
