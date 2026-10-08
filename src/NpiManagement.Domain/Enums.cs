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
}

public enum InformationRequestStatus
{
    Pending = 0,
    Resolved = 1,
}

/// <summary>Rol bajo el cual se aprueba una versión de PrintCard. "Client" siempre lo ejecuta un usuario Comercial (decisión 8 — el cliente no es un actor autenticado).</summary>
public enum ApproverRole
{
    Design = 0,
    Engineering = 1,
    Client = 2,
}

public enum ChangeRequestOrigin
{
    Internal = 0,
    ClientReported = 1,
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
