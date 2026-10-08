using NpiManagement.Application.Authorization;

namespace NpiManagement.Application.Abstractions;

/// <summary>Opción tal como la ve quien administra: si está protegida (y por qué) y cuántas veces la usan los NPIs.</summary>
public sealed record CatalogAdminOptionDto(Guid Id, string Value, int DisplayOrder, bool IsActive, string? ProtectedReason, int UsageCount);

public sealed record CatalogAdminDto(Guid Id, string Key, string DisplayName, string? Hint, IReadOnlyList<CatalogAdminOptionDto> Options);

/// <summary>Una entrada del historial de cambios de catálogos.</summary>
public sealed record CatalogChangeDto(Guid Id, string CatalogKey, string Action, string? OldValue, string? NewValue, string ChangedByName, DateTimeOffset ChangedAt);

/// <summary>
/// Administración de las opciones de los combobox del formulario (lo pidió Ingeniería). Renombrar o
/// desactivar sólo afecta capturas nuevas: los NPIs guardan el texto que se capturó y lo conservan.
/// Cada cambio queda en el historial con quién y cuándo.
/// </summary>
public interface ICatalogAdminService
{
    Task<IReadOnlyList<CatalogAdminDto>> ListAsync(CancellationToken cancellationToken);

    Task<CatalogAdminOptionDto> AddOptionAsync(Guid catalogId, string value, CurrentUser actor, string actorName, CancellationToken cancellationToken);

    Task RenameOptionAsync(Guid optionId, string value, CurrentUser actor, string actorName, CancellationToken cancellationToken);

    Task SetActiveAsync(Guid optionId, bool isActive, CurrentUser actor, string actorName, CancellationToken cancellationToken);

    /// <summary>Nuevo orden: todas las opciones del catálogo, en el orden deseado.</summary>
    Task ReorderAsync(Guid catalogId, IReadOnlyList<Guid> optionIds, CurrentUser actor, string actorName, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogChangeDto>> ListChangesAsync(string? catalogKey, CancellationToken cancellationToken);
}
