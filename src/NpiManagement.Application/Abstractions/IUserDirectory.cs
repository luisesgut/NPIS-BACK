using NpiManagement.Domain;

namespace NpiManagement.Application.Abstractions;

/// <param name="DirectorUserId">Director de Comercial del vendedor ("Mi equipo", 2026-11-18).</param>
public sealed record UserSummaryDto(Guid Id, string FullName, string Email, string Role, AreaType Area, UserStatus Status, Guid? DirectorUserId = null);

/// <summary>Usuarios consultables por área, para asignaciones (docs/api-contract.md §2).</summary>
public interface IUserDirectory
{
    Task<IReadOnlyList<UserSummaryDto>> ListAsync(AreaType? area, CancellationToken cancellationToken);

    /// <summary>Sólo Id y nombre de los usuarios indicados. Los ids que no existen no aparecen en el resultado.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Área y estatus de un usuario, para validar asignaciones. null si no existe.</summary>
    Task<UserSummaryDto?> FindAsync(Guid userId, CancellationToken cancellationToken);
}
