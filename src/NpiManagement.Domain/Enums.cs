namespace NpiManagement.Domain;

/// <summary>Estado de alto nivel del ciclo de vida del NPI (ADR-015).</summary>
public enum LifecycleStatus
{
    Draft = 0,
    Active = 1,
    Completed = 2,
    Rejected = 3,
    Cancelled = 4,
}

/// <summary>Motivo de un rechazo terminal (sólo aplica cuando LifecycleStatus == Rejected).</summary>
public enum TerminalReason
{
    NoFeasibility = 0,
    SuppliesRejectionTotal = 1,
    ClientRejection = 2,
    /// <summary>Diseño declaró el NPI no factible (rechazo terminal de Diseño).</summary>
    DesignNoFeasibility = 3,
    /// <summary>El cliente rechazó el precio de la cotización de SISPRO (rechazo terminal de Comercial).</summary>
    PriceRejection = 4,
}

/// <summary>
/// Estado de una cotización de SISPRO ligada al NPI. Sólo la versión vigente (la de número más
/// alto) cuenta para el flujo; las anteriores quedan como historial.
/// </summary>
public enum SisproQuoteStatus
{
    /// <summary>Ligada y enviada: falta la respuesta del cliente al precio.</summary>
    AwaitingClient = 0,
    Accepted = 1,
    /// <summary>El cliente pidió ajustar el precio: Comercial recotiza en SISPRO y liga la nueva.</summary>
    AdjustmentRequested = 2,
    /// <summary>Se ligó otra cotización antes de que el cliente respondiera a ésta.</summary>
    Superseded = 3,
    /// <summary>Cambió la estructura del NPI después de cotizar: hay que recotizar.</summary>
    Invalidated = 4,
    /// <summary>El cliente rechazó el precio (el NPI se cierra por PriceRejection).</summary>
    Rejected = 5,
}

/// <summary>Estado de una propuesta de cierre de un usuario normal (ClosureProposal).</summary>
public enum ClosureProposalStatus
{
    /// <summary>Espera el visto bueno del admin del área.</summary>
    Pending = 0,
    /// <summary>El admin dio luz verde: el NPI se cerró con ese motivo.</summary>
    Confirmed = 1,
    /// <summary>El admin no lo autorizó: el NPI sigue su curso.</summary>
    Declined = 2,
    /// <summary>Quien la propuso la retiró antes de que el admin decidiera.</summary>
    Withdrawn = 3,
    /// <summary>El NPI se cerró por otra vía (otro motivo o cancelación) antes de que el admin decidiera.</summary>
    Superseded = 4,
}

/// <summary>Estado operativo de presentación para el área de Ingeniería (no gatilla por sí solo las transiciones).</summary>
public enum EngineeringAreaStatus
{
    Requested = 0,
    Assigned = 1,
    FeasibilityApproved = 2,
    ChangesRequested = 3,
    NoFeasibility = 4,
    SuppliesTemporaryRejection = 5,
    PtRegistered = 6,
    EtCreated = 7,
    EtLoadedInSispro = 8,
}

/// <summary>Estado operativo de presentación para el área de Diseño.</summary>
public enum DesignAreaStatus
{
    Requested = 0,
    Assigned = 1,
    FeasibilityApproved = 2,
    ArtesValidadas = 3,
    AwaitingInformation = 4,
    PrintCardInApproval = 5,
    PrintCardLoaded = 6,
    /// <summary>Diseño rechazó con cambios: Comercial debe corregir la captura y reenviar.</summary>
    ChangesRequested = 7,
    NoFeasibility = 8,
    /// <summary>Diseño rechazó las artes del cliente: Comercial debe subir artes nuevas.</summary>
    ArtworkRejected = 9,
    /// <summary>Ingeniería rechazó el PrintCard o el cliente pidió cambios: Diseño debe generar una versión nueva.</summary>
    PrintCardChangesRequested = 10,
}

public enum AreaType
{
    Comercial = 0,
    Ingenieria = 1,
    Diseno = 2,
    Total = 3,
}

public enum Priority
{
    Baja = 0,
    Media = 1,
    Alta = 2,
}

public enum AttachmentContext
{
    Comment = 0,
    ChangeRequest = 1,
    Feasibility = 2,
    ClientApproval = 3,
    InfoRequest = 4,
    PtSap = 5,
    Insumos = 6,
    PrintCard = 7,
    ClientRejection = 8,
    /// <summary>Artes o archivos de referencia del NPI (típicamente links a archivos pesados).</summary>
    Artwork = 9,
}

public enum TimerStage
{
    Global = 0,
    Commercial = 1,
    Engineering = 2,
    Design = 3,
}

public enum TimerAction
{
    Start = 0,
    Pause = 1,
    Resume = 2,
    Close = 3,
    StageChange = 4,
}

public enum InformationRequestType
{
    Client = 0,
    Commercial = 1,
    /// <summary>Dudas de Diseño que sólo Ingeniería puede aclarar (2026-11-04).</summary>
    Engineering = 2,
}

public enum InformationRequestStatus
{
    Pending = 0,
    Resolved = 1,
    /// <summary>Diseño la retiró antes de que se la contestaran (ya no hacía falta).</summary>
    Withdrawn = 2,
}

/// <summary>Rol bajo el cual se aprueba una versión de PrintCard. "Client" siempre lo ejecuta un usuario Comercial (decisión 8 — el cliente no es un actor autenticado).</summary>
public enum ApproverRole
{
    Design = 0,
    Engineering = 1,
    Client = 2,
}

/// <summary>Complejidad del arte según Diseño (AA la más alta). Se captura al aprobar la factibilidad de Diseño.</summary>
public enum ArtClassification
{
    AA = 0,
    A = 1,
    B = 2,
    C = 3,
}

public enum ChangeRequestOrigin
{
    Internal = 0,
    ClientReported = 1,
    /// <summary>Diseño rechazó las artes; se resuelve cuando Comercial sube artes nuevas.</summary>
    ArtworkRejected = 2,
    /// <summary>
    /// Comercial→Ingeniería: Comercial cambió campos que Ingeniería ya había aprobado; se resuelve cuando
    /// Ingeniería reconfirma su factibilidad (Npi.ReconfirmEngineeringFeasibility). Máx. 20 caracteres en BD.
    /// </summary>
    Reconfirmation = 3,
    /// <summary>Comercial→Comercial: Comercial modifica el NPI por su cuenta (Npi.StartCommercialEdit); se resuelve al reenviar.</summary>
    CommercialEdit = 4,
}

/// <summary>
/// Estado de la cuenta de un usuario. Sustituye al antiguo <c>IsActive</c> booleano, que no podía
/// distinguir "se registró y espera aprobación" de "un admin lo dio de baja" — dos situaciones
/// que merecen mensajes distintos en el login y aparecen en pantallas distintas del panel.
/// </summary>
public enum UserStatus
{
    /// <summary>Se registró por su cuenta; todavía no tiene rol y no puede iniciar sesión.</summary>
    Pending = 0,

    /// <summary>Aprobado por un administrador, con rol y área asignados.</summary>
    Active = 1,

    /// <summary>Dado de baja o rechazado por un administrador. No puede iniciar sesión.</summary>
    Disabled = 2,
}

/// <summary>Qué envía un usuario desde el botón "Reportar" de la app.</summary>
public enum FeedbackKind
{
    /// <summary>Algo no funciona como debería.</summary>
    Bug = 0,

    /// <summary>Una idea para mejorar el sistema.</summary>
    Suggestion = 1,

    /// <summary>El usuario no sabe cómo hacer algo.</summary>
    Question = 2,
}

/// <summary>Seguimiento que el AdminTotal le da a un reporte.</summary>
public enum FeedbackStatus
{
    New = 0,
    InReview = 1,
    Resolved = 2,
    Dismissed = 3,
}
