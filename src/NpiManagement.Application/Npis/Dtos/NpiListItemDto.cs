namespace NpiManagement.Application.Npis.Dtos;

/// <summary>Fila resumida para tablas/paneles — ver docs/api-contract.md §3.2. Incluye los 4
/// cronómetros (Fase 8): son columnas propias del Npi, baratas de traer en el mismo listado, y el
/// frontend los necesita para pintar los chips de tiempo sin una consulta por fila.</summary>
public sealed record NpiListItemDto(
    Guid Id,
    string? NpiNumber,
    string Priority,
    string LifecycleStatus,
    string EngineeringAreaStatus,
    string DesignAreaStatus,
    string Cliente,
    string NumeroCotizacion,
    string DescripcionProducto,
    DateOnly RequestDate,
    Guid CreatedByUserId,
    Guid? AssignedEngineeringUserId,
    Guid? AssignedDesignUserId,
    Guid AssignedCommercialUserId,
    TimerDto GlobalTimer,
    TimerDto CommercialTimer,
    TimerDto EngineeringTimer,
    TimerDto DesignTimer,
    /// <summary>Etapas completadas de las 8 de la ruta (NpiStageMilestones), para pintar el avance sin pedir el detalle de cada fila.</summary>
    int CompletedStages);
