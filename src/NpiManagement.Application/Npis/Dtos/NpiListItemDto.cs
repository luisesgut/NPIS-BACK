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
    int CompletedStages,
    /// <summary>Área ("Comercial", "Ingenieria", "Diseno") de un cierre que espera visto bueno del admin; null si no hay.</summary>
    string? PendingClosureArea = null,
    /// <summary>Clasificación del arte (AA, A, B, C) y valores LAB: el diseñador la ve en su panel sin abrir el detalle.</summary>
    string? ArtClassification = null,
    bool? HasLabValues = null,
    /// <summary>Nombres de los responsables, para que los paneles los muestren sin pedir el detalle de cada NPI.</summary>
    string? AssignedCommercialName = null,
    string? AssignedEngineeringName = null,
    string? AssignedDesignName = null,
    /// <summary>Código del PrintCard (el de la versión final si ya hay; si no, el de la más reciente) y código PT de SAP.</summary>
    string? PrintCardCode = null,
    string? CodigoPT = null,
    /// <summary>Número de la versión final del PrintCard (la cargada en sistema); null mientras no hay.</summary>
    int? PrintCardFinalVersion = null,
    /// <summary>
    /// Con quién está la versión vigente del PrintCard (2026-11-09; Comercial no distinguía "en aprobación"):
    /// "Ingenieria" (la revisa), "PorEnviar" (Ingeniería la liberó; Comercial la manda), "Cliente" (enviada,
    /// falta su respuesta), "Aprobado" (el cliente la aprobó; falta cargarla), "RechazadoIngenieria",
    /// "CambiosCliente" (el cliente pidió cambios), "Rehacer" (se retiró porque cambió la información). Null si no aplica.
    /// </summary>
    string? PrintCardStep = null,
    /// <summary>A quién espera Diseño con una solicitud de información: "Comercial", "Ingenieria", "Ambas" o null.</summary>
    string? PendingInfoFrom = null,
    /// <summary>Ingeniería debe reconfirmar su factibilidad (Comercial cambió campos que ya revisó).</summary>
    bool PendingReconfirmation = false,
    /// <summary>Comercial abrió "Modificar NPI" y no lo ha reenviado.</summary>
    bool CommercialEditing = false,
    /// <summary>Equipo del NPI con nombres (2026-11-10), para mostrarlo junto al responsable.</summary>
    IReadOnlyList<Queries.NpiTeamMemberDto>? Team = null);
