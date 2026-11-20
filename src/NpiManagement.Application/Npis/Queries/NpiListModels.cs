namespace NpiManagement.Application.Npis.Queries;

/// <param name="LifecycleStatus">Uno o varios estatus separados por coma (p. ej. "Draft,Active").</param>
/// <param name="Cliente">Cliente exacto (las gráficas del dashboard filtran por él); Search sigue siendo parcial.</param>
/// <param name="Situation">"¿Qué necesita?" del panel (NpiSituation: mine, late, waiting, changes, reconfirmation); sólo activos.</param>
/// <param name="Stage">"¿Dónde está?" del panel (NpiStageGroup: factibilidad, printcard, cliente, alta, pedido); sólo activos.</param>
/// <param name="Area">Área del panel para Situation; sólo cuenta para AdminTotal y Director.</param>
/// <param name="OnlyIds">Uso interno: el handler resuelve Situation/Stage a los NPIs que cumplen.</param>
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
    string? PendingClosureArea = null,
    string? Situation = null,
    string? Stage = null,
    string? Area = null,
    IReadOnlyCollection<Guid>? OnlyIds = null,
    /// <summary>
    /// "Mis NPIs" del admin de un área (2026-11-11). Comercial: los que capturó, tiene asignados o en los
    /// que está en el equipo. Ingeniería/Diseño: los suyos (responsable o equipo) más los que faltan por
    /// asignar en esa área, porque asignar es su trabajo. Lo llena el handler con el token, nunca el cliente.
    /// </summary>
    Guid? MineUserId = null,
    Domain.AreaType? MineArea = null,
    /// <summary>"Mi equipo": NPIs capturados o asignados (Comercial) a estas personas. Lo llena el handler.</summary>
    IReadOnlyCollection<Guid>? TeamUserIds = null,
    /// <summary>"Partir de un NPI anterior": NPIs capturados por alguna de estas personas (el vendedor y su director). Lo llena el handler.</summary>
    IReadOnlyCollection<Guid>? CreatedByAnyOf = null)
{
    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => PageSize is < 1 or > 100 ? 20 : PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
