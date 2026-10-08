namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>Historial de cambios a los catálogos: quién agregó, renombró, activó/desactivó o reordenó qué.</summary>
public sealed class CatalogChangeLog
{
    public Guid Id { get; set; }
    public Guid CatalogId { get; set; }
    public string CatalogKey { get; set; } = string.Empty;
    /// <summary>Added, Renamed, Activated, Deactivated o Reordered.</summary>
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
}
