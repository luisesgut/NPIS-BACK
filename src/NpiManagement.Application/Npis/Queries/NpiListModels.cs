namespace NpiManagement.Application.Npis.Queries;

/// <param name="LifecycleStatus">Uno o varios estatus separados por coma (p. ej. "Draft,Active").</param>
/// <param name="Cliente">Cliente exacto (las gráficas del dashboard filtran por él); Search sigue siendo parcial.</param>
/// <param name="PendingClosureArea">Sólo NPIs con un cierre esperando visto bueno de esa área ("Comercial", "Ingenieria", "Diseno") o de cualquiera ("Any").</param>
public sealed record NpiListFilter(
    string? Search,
    string? LifecycleStatus,
    Guid? AssignedToUserId,
    Guid? CreatedByUserId,
    string? Priority,
    string? Cliente = null,
    int Page = 1,
    int PageSize = 20,
    string? PendingClosureArea = null)
{
    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => PageSize is < 1 or > 100 ? 20 : PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
