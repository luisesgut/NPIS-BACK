using NpiManagement.Domain.Common;
using NpiManagement.Domain.Entities;
using NpiManagement.Domain.Events;
using NpiManagement.Domain.Exceptions;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Domain;

/// <summary>
/// Agregado raíz del sistema de Gestión de NPIs. Única puerta de escritura para los datos de
/// captura, el ciclo de vida, los estados por área, los hitos del flujo, los cuatro cronómetros
/// y las colecciones satélite. Ver docs/domain-model.md y docs/workflow.md (catálogo completo
/// de las 29 acciones implementadas aquí como métodos).
/// </summary>
public sealed class Npi
{
    public Guid Id { get; }
    public NpiNumber? NpiNumber { get; private set; }
    public Priority Priority { get; private set; }
    public NpiCaptureData Capture { get; private set; } = NpiCaptureData.Empty;

    public LifecycleStatus LifecycleStatus { get; private set; }
    public TerminalReason? TerminalReason { get; private set; }
    public EngineeringAreaStatus EngineeringAreaStatus { get; private set; }
    public DesignAreaStatus DesignAreaStatus { get; private set; }

    public Guid CreatedByUserId { get; }
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Foto de la captura tal como se envió por primera vez (la "fase inicial"), para compararla
    /// contra la actual. null en NPIs enviados antes de que existiera la bitácora de cambios.
    /// </summary>
    public string? InitialCaptureJson { get; private set; }
    public DateTimeOffset? FirstSubmittedAt { get; private set; }
    public Guid? FirstSubmittedByUserId { get; private set; }
    public DateOnly RequestDate { get; }

    public Guid? AssignedEngineeringUserId { get; private set; }
    public Guid? AssignedDesignUserId { get; private set; }
    public Guid AssignedCommercialUserId { get; private set; }

    public TimerState GlobalTimer { get; private set; } = TimerState.NotStarted;
    public TimerState CommercialTimer { get; private set; } = TimerState.NotStarted;
    public TimerState EngineeringTimer { get; private set; } = TimerState.NotStarted;
    public TimerState DesignTimer { get; private set; } = TimerState.NotStarted;

    public DateTimeOffset? ArtesValidadasAt { get; private set; }
    public Guid? ArtesValidadasByUserId { get; private set; }

    /// <summary>Clasificación del arte que da Diseño (AA, A, B, C). Obligatoria al aprobar su factibilidad.</summary>
    public ArtClassification? ArtClassification { get; private set; }
    /// <summary>Indicaciones del admin de Diseño para el diseñador (2026-11-09): qué tomar en cuenta del arte. null = sin indicaciones.</summary>
    public string? DesignInstructions { get; private set; }
    /// <summary>Sólo con Base Pantone: si el cliente tiene valores LAB. Null cuando no aplica.</summary>
    public bool? HasLabValues { get; private set; }

    public DateTimeOffset? PrintCardLoadedAt { get; private set; }
    public Guid? PrintCardLoadedByUserId { get; private set; }
    public Guid? PrintCardLoadedVersionId { get; private set; }

    /// <summary>
    /// La versión final del PrintCard: la que Diseño cargó en sistema (con las 3 aprobaciones). null
    /// mientras no se carga. Los NPIs cargados antes de guardar la versión toman la más reciente.
    /// </summary>
    public PrintCardVersion? FinalPrintCardVersion =>
        PrintCardLoadedAt is null ? null : _printCardVersions.FirstOrDefault(v => v.Id == PrintCardLoadedVersionId) ?? CurrentPrintCardVersion;

    public DateTimeOffset? PrintCardSentToClientAt { get; private set; }
    public Guid? PrintCardSentToClientByUserId { get; private set; }

    public string? CodigoPT { get; private set; }
    public string? PtName { get; private set; }
    /// <summary>Tabla copiada de Excel/SAP al cargar el PT (texto separado por tabuladores, 2026-11-09). null si no se pegó.</summary>
    public string? PtSapTable { get; private set; }
    public const int PtSapTableMaxLength = 20000;
    public DateTimeOffset? PtRegisteredAt { get; private set; }
    public Guid? PtRegisteredByUserId { get; private set; }

    public string? PedidoSapNumber { get; private set; }
    public DateTimeOffset? PedidoSapRegisteredAt { get; private set; }
    public Guid? PedidoSapRegisteredByUserId { get; private set; }

    public DateTimeOffset? EtCreatedAt { get; private set; }
    public Guid? EtCreatedByUserId { get; private set; }
    public DateTimeOffset? EtLoadedInSisproAt { get; private set; }
    public Guid? EtLoadedInSisproByUserId { get; private set; }

    public SuppliesTemporaryRejectionInfo? SuppliesTemporaryRejection { get; private set; }
    public SuppliesAgreementInfo? SuppliesAgreement { get; private set; }
    public ClientRejectionInfo? ClientRejection { get; private set; }

    private readonly List<Attachment> _attachments = [];
    private readonly List<ChangeRequest> _changeRequests = [];
    private readonly List<InformationRequest> _informationRequests = [];
    private readonly List<FeasibilityReview> _feasibilityReviews = [];
    private readonly List<PrintCardVersion> _printCardVersions = [];
    private readonly List<SisproQuoteLink> _sisproQuotes = [];
    private readonly List<ClosureProposal> _closureProposals = [];
    private readonly List<TimerEvent> _timerEvents = [];
    private readonly List<AuditEvent> _auditEvents = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<Attachment> Attachments => _attachments;
    public IReadOnlyCollection<ChangeRequest> ChangeRequests => _changeRequests;
    public IReadOnlyCollection<InformationRequest> InformationRequests => _informationRequests;
    public IReadOnlyCollection<FeasibilityReview> FeasibilityReviews => _feasibilityReviews;
    public IReadOnlyCollection<PrintCardVersion> PrintCardVersions => _printCardVersions;
    public IReadOnlyCollection<SisproQuoteLink> SisproQuotes => _sisproQuotes;
    /// <summary>Propuestas de cierre de usuarios normales y su decisión (historial completo).</summary>
    public IReadOnlyCollection<ClosureProposal> ClosureProposals => _closureProposals;
    public IReadOnlyCollection<TimerEvent> TimerEventLog => _timerEvents;
    public IReadOnlyCollection<AuditEvent> AuditLog => _auditEvents;
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    // Por número de versión, nunca por posición: EF Core no garantiza el orden en que carga la
    // colección (las llaves son GUID), así que "la última de la lista" podía ser la v1.
    public PrintCardVersion? CurrentPrintCardVersion => _printCardVersions.MaxBy(v => v.VersionNumber);

    public FeasibilityReview? EngineeringFeasibility => _feasibilityReviews.FirstOrDefault(f => f.Area == AreaType.Ingenieria);
    public FeasibilityReview? DesignFeasibility => _feasibilityReviews.FirstOrDefault(f => f.Area == AreaType.Diseno);

    /// <summary>La cotización de SISPRO vigente (por número de versión, igual que el PrintCard).</summary>
    public SisproQuoteLink? CurrentSisproQuote => _sisproQuotes.MaxBy(q => q.VersionNumber);
    public bool SisproQuoteAccepted => CurrentSisproQuote?.Status == SisproQuoteStatus.Accepted;

    private Npi(Guid id, Guid createdByUserId, DateTimeOffset createdAt, NpiCaptureData capture, Priority priority, Guid? preAssignedEngineeringUserId)
    {
        Id = id;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        RequestDate = DateOnly.FromDateTime(createdAt.UtcDateTime);
        Capture = capture.WithUppercaseNames();
        Priority = priority;
        LifecycleStatus = LifecycleStatus.Draft;
        EngineeringAreaStatus = EngineeringAreaStatus.Requested;
        DesignAreaStatus = DesignAreaStatus.Requested;
        AssignedCommercialUserId = createdByUserId;
        AssignedEngineeringUserId = preAssignedEngineeringUserId;
    }

    /// <summary>
    /// Constructor sin parámetros reservado para la materialización de EF Core (Infrastructure).
    /// Domain no referencia EF Core en absoluto — este constructor sólo evita que EF tenga que
    /// resolver enlace de constructor con parámetros (incluido un tipo de propiedad de valor
    /// como Capture), que es más frágil. Todas las propiedades (incluidas las de sólo "get")
    /// se rellenan por reflexión sobre su campo de respaldo al hidratar desde la base de datos.
    /// </summary>
    private Npi()
    {
    }

    /// <summary>Acción 1 — Crear borrador.</summary>
    public static Npi CreateDraft(
        Guid id,
        Actor actor,
        IClock clock,
        NpiCaptureData capture,
        Priority priority,
        Guid? preAssignedEngineeringUserId = null)
    {
        var now = clock.UtcNow;
        var npi = new Npi(id, actor.UserId, now, capture, priority, preAssignedEngineeringUserId);
        npi.Audit(actor, now, "NpiCreated", null);
        return npi;
    }

    /// <summary>
    /// Acción 2 — Editar la captura (borrador, o tras una solicitud de cambios). Desde el primer
    /// envío, cada campo que cambia queda en la bitácora con su valor anterior, quién lo cambió y
    /// qué solicitud lo pidió; quien llama persiste los <see cref="CaptureChange"/> devueltos. Los
    /// borradores no se registran: hasta enviarse, el NPI sólo existe para Comercial.
    /// </summary>
    public IReadOnlyList<CaptureChange> UpdateDraft(Actor actor, IClock clock, NpiCaptureData capture, Priority priority)
    {
        EnsureNotTerminal();
        // Clientes, productos y contactos en MAYÚSCULAS (2026-11-09).
        capture = capture.WithUppercaseNames();
        var changes = new List<CaptureChange>();

        if (LifecycleStatus != Domain.LifecycleStatus.Draft)
        {
            var now = clock.UtcNow;
            var openRequests = _changeRequests
                .Where(c => c.ToArea == AreaType.Comercial && !c.IsResolved && c.Origin != ChangeRequestOrigin.ArtworkRejected)
                .OrderByDescending(c => c.RequestedAt)
                .ToList();

            var diffs = CaptureDiff.Between(Capture, capture).ToList();
            if (Priority != priority) diffs.Add(new FieldChange("priority", Priority.ToString(), priority.ToString()));

            foreach (var diff in diffs)
            {
                // Quién lo pidió: la solicitud abierta que marcó ese campo; si ninguna lo marcó pero
                // había una abierta, el cambio se hizo al atenderla (no "por iniciativa propia").
                var requestedBy = openRequests.FirstOrDefault(r => r.FieldKeys.Contains(diff.Field));
                var related = requestedBy ?? openRequests.FirstOrDefault();
                changes.Add(new CaptureChange(Guid.NewGuid(), Id, diff.Field, diff.Before, diff.After, actor.UserId, actor.Name, now, related?.Id, requestedBy is not null));
            }

            if (changes.Count > 0)
                Audit(actor, now, "CaptureUpdated", $"{changes.Count} campo{(changes.Count == 1 ? "" : "s")}");

            // Lo que se cotizó ya no es lo que se va a fabricar: la cotización vigente deja de valer.
            var structureChanges = diffs.Select(d => d.Field).Where(QuotedStructureFields.Contains).ToList();
            if (structureChanges.Count > 0 && CurrentSisproQuote is { Status: SisproQuoteStatus.AwaitingClient or SisproQuoteStatus.Accepted } quote)
            {
                var reason = $"Cambió la estructura después de cotizar: {string.Join(", ", structureChanges)}";
                quote.Invalidate(now, reason);
                Audit(actor, now, "SisproQuoteInvalidated", $"cotizacion={quote.NumeroCotizacion}; {reason}");
            }
        }

        Capture = capture;
        Priority = priority;
        return changes;
    }

    /// <summary>Acción 3 — Enviar NPI. Primer envío requiere el folio ya asignado por Infrastructure (SEQUENCE de SQL Server).</summary>
    public void Submit(Actor actor, IClock clock, IBusinessCalendar calendar, ValueObjects.NpiNumber? assignedNumberIfFirstSubmission = null, string? comment = null)
    {
        var now = clock.UtcNow;

        if (LifecycleStatus == Domain.LifecycleStatus.Draft)
        {
            if (assignedNumberIfFirstSubmission is null)
                throw new InvalidNpiTransitionException("npi-number-required", "El primer envío requiere un NpiNumber ya asignado por Infrastructure.");

            NpiNumber = assignedNumberIfFirstSubmission;
            LifecycleStatus = Domain.LifecycleStatus.Active;
            InitialCaptureJson = CaptureDiff.Snapshot(Capture);
            FirstSubmittedAt = now;
            FirstSubmittedByUserId = actor.UserId;
            EngineeringAreaStatus = AssignedEngineeringUserId is not null ? EngineeringAreaStatus.Assigned : EngineeringAreaStatus.Requested;

            GlobalTimer = GlobalTimer.StartOrResume(clock);
            EngineeringTimer = EngineeringTimer.StartOrResume(clock);
            AppendTimerEvent(TimerStage.Global, TimerAction.Start, now, actor.UserId);
            AppendTimerEvent(TimerStage.Engineering, TimerAction.Start, now, actor.UserId);

            Audit(actor, now, "NpiSubmitted", null);
            _domainEvents.Add(new NpiSubmittedEvent(Id, now));
            return;
        }

        // Reenvío tras "Rechazar con cambios" de Ingeniería, de Diseño o de ambos: Comercial corrige la
        // captura una sola vez y el mismo reenvío atiende las dos solicitudes.
        var engineeringChangesPending = EngineeringAreaStatus == EngineeringAreaStatus.ChangesRequested;
        var designChangesPending = PendingDesignChangeRequest is not null;
        var commercialEdit = PendingCommercialEdit;
        if (LifecycleStatus == Domain.LifecycleStatus.Active && (engineeringChangesPending || designChangesPending || commercialEdit is not null))
        {
            if (comment is { Length: > 2000 })
                throw new NpiValidationException("resubmit-comment-too-long", "El comentario no puede exceder 2000 caracteres.");

            var resolved = new List<ChangeRequest>();
            if (engineeringChangesPending)
            {
                // Registra qué corrigió Comercial respecto a lo que Ingeniería vio al pedir los cambios.
                if (PendingEngineeringChangeRequest is { } engineeringRequest)
                {
                    engineeringRequest.Resolve(actor, now, comment, Capture);
                    resolved.Add(engineeringRequest);
                }
                // Si ya había dado factibilidad (p. ej. al reconfirmar), regresa a ella y no a "Asignado".
                EngineeringAreaStatus = CurrentEngineeringAreaStatus();
                if (EngineeringHasPendingWork) ResumeTimer(TimerStage.Engineering, actor, clock, now);
                if (EngineeringFeasibility is not null && DesignHasPendingWork) ResumeDesignTimer(actor, clock, now);
            }

            if (designChangesPending)
            {
                var designRequest = PendingDesignChangeRequest!;
                designRequest.Resolve(actor, now, comment, Capture);
                resolved.Add(designRequest);
                DesignAreaStatus = CurrentDesignAreaStatus();
                // Después de su factibilidad, Diseño sólo retoma si de verdad le toca (no si espera a Ingeniería).
                if (DesignFeasibility is null || DesignHasPendingWork) ResumeDesignTimer(actor, clock, now);
            }

            if (commercialEdit is not null)
            {
                commercialEdit.Resolve(actor, now, comment, Capture);
                resolved.Add(commercialEdit);
                // Si Diseño esperaba algo del cliente, la modificación lo resuelve: se cierra con lo que cambió.
                if (_informationRequests.FirstOrDefault(r => r.Type == InformationRequestType.Commercial && r.Status == InformationRequestStatus.Pending) is { } info)
                {
                    var count = commercialEdit.ChangesApplied.Count;
                    var answer = $"Lo resolví modificando el NPI ({count} campo{(count == 1 ? "" : "s")} cambiado{(count == 1 ? "" : "s")}; el detalle está en el historial).";
                    info.Resolve(now, actor.UserId, string.IsNullOrWhiteSpace(comment) ? answer : $"{answer} {comment.Trim()}");
                    Audit(actor, now, "CommercialInformationSent", answer);
                }
                DesignAreaStatus = CurrentDesignAreaStatus();
                if (DesignHasPendingWork) ResumeDesignTimer(actor, clock, now);
                if (EngineeringHasPendingWork) ResumeTimer(TimerStage.Engineering, actor, clock, now);
            }

            // Ingeniería ya había dado factibilidad y Comercial tocó campos que ella revisó: debe reconfirmarla.
            if (EngineeringFeasibility is not null) RequestEngineeringReconfirmation(actor, clock, now, resolved);

            if (!CommercialHasPendingWork) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);

            var areas = commercialEdit is not null ? "Comercial" : (engineeringChangesPending, designChangesPending) switch
            {
                (true, true) => "Ingenieria,Diseno",
                (false, true) => "Diseno",
                _ => null,
            };
            Audit(actor, now, "NpiResubmittedAfterChanges", areas);
            return;
        }

        throw new InvalidNpiTransitionException("npi-not-submittable", "El NPI no está en un estado que admita envío.");
    }

    /// <summary>
    /// Acción 4 — Asignar (o reasignar) a Ingeniería. Reasignar sólo cambia a la persona: el estado
    /// del área se queda donde iba (antes regresaba a "Asignado" y se perdía en qué paso estaba).
    /// </summary>
    public void AssignEngineering(Guid userId, Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        var now = clock.UtcNow;
        var previous = AssignedEngineeringUserId;
        AssignedEngineeringUserId = userId;
        if (EngineeringAreaStatus == EngineeringAreaStatus.Requested) EngineeringAreaStatus = EngineeringAreaStatus.Assigned;
        Audit(actor, now, previous is null ? "EngineeringAssigned" : "EngineeringReassigned", $"from={previous};to={userId}");
    }

    /// <summary>
    /// Acción 5 — Asignar (o reasignar) a Diseño. El admin de Diseño puede clasificar el arte (y los
    /// valores LAB) en el mismo paso, para que el diseñador lo reciba ya clasificado. Igual que en
    /// Ingeniería, reasignar no regresa el estado del área.
    /// </summary>
    public void AssignDesign(Guid userId, Actor actor, IClock clock, ArtClassification? artClassification = null, bool? hasLabValues = null)
    {
        EnsureNotTerminal();
        if (artClassification is not null) ApplyDesignClassification(artClassification, hasLabValues);
        var now = clock.UtcNow;
        var previous = AssignedDesignUserId;
        AssignedDesignUserId = userId;
        if (DesignAreaStatus == DesignAreaStatus.Requested) DesignAreaStatus = DesignAreaStatus.Assigned;
        Audit(actor, now, previous is null ? "DesignAssigned" : "DesignReassigned", $"from={previous};to={userId}");
        if (artClassification is not null) Audit(actor, now, "DesignClassificationUpdated", DescribeDesignClassification());
    }

    /// <summary>Acción 6 — Reasignar Comercial.</summary>
    public void ReassignCommercial(Guid userId, Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        var now = clock.UtcNow;
        var previous = AssignedCommercialUserId;
        AssignedCommercialUserId = userId;
        Audit(actor, now, "CommercialReassigned", $"from={previous};to={userId}");
    }

    /// <summary>Acción 7 — Factibilidad de Ingeniería. Independiente de la de Diseño (decisión 1).</summary>
    public void ApproveEngineeringFeasibility(Actor actor, IClock clock, IBusinessCalendar calendar, string? note, IReadOnlyList<string>? confirmedFieldKeys = null)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Ingenieria);
        if (AssignedEngineeringUserId is null)
            throw new InvalidNpiTransitionException("engineering-not-assigned", "El NPI no tiene un ingeniero asignado.");
        if (EngineeringFeasibility is not null)
            throw new InvalidNpiTransitionException("engineering-feasibility-already-approved", "La factibilidad de Ingeniería ya fue aprobada.");

        var now = clock.UtcNow;
        _feasibilityReviews.Add(new FeasibilityReview(Guid.NewGuid(), AreaType.Ingenieria, actor.UserId, actor.Name, now, note, confirmedFieldKeys));
        EngineeringAreaStatus = EngineeringAreaStatus.FeasibilityApproved;

        // Con la factibilidad aprobada, la pelota pasa a Diseño/Comercial/cliente (PrintCard):
        // Ingeniería no carga ese tiempo. Se reanuda al cargar el PrintCard en sistema.
        PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);
        TryStartDesignTimer(actor, clock, now);
        // Con la estructura validada, a Comercial le toca cotizarla en SISPRO.
        if (AwaitingSisproQuote) RunCommercialTimer(actor, clock, now, "Factibilidad aprobada: cotizar en SISPRO");

        Audit(actor, now, "EngineeringFeasibilityApproved", note);
        _domainEvents.Add(new EngineeringFeasibilityApprovedEvent(Id, now));
    }

    /// <summary>Acción 8 — Factibilidad de Diseño. Independiente de la de Ingeniería (decisión 1).</summary>
    public void ApproveDesignFeasibility(Actor actor, IClock clock, string? note, ArtClassification? artClassification, bool? hasLabValues = null)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Diseno);
        if (AssignedDesignUserId is null)
            throw new InvalidNpiTransitionException("design-not-assigned", "El NPI no tiene un diseñador asignado.");
        if (DesignFeasibility is not null)
            throw new InvalidNpiTransitionException("design-feasibility-already-approved", "La factibilidad de Diseño ya fue aprobada.");
        if (PendingDesignChangeRequest is not null)
            throw new InvalidNpiTransitionException("design-changes-pending", "Comercial aún no atiende los cambios que pidió Diseño.");
        // Si el admin ya lo clasificó al asignar, el diseñador aprueba con esa clasificación: cambiarla
        // es sólo del admin de Diseño (2026-11-04). Si no hay clasificación, el diseñador la captura aquí.
        if (ArtClassification is not null && !IsDesignAdmin(actor)
            && ((artClassification is not null && artClassification != ArtClassification) || (hasLabValues is not null && hasLabValues != HasLabValues)))
            throw DesignClassificationAdminOnly();
        ApplyDesignClassification(artClassification ?? ArtClassification, hasLabValues ?? HasLabValues);

        var now = clock.UtcNow;
        _feasibilityReviews.Add(new FeasibilityReview(Guid.NewGuid(), AreaType.Diseno, actor.UserId, actor.Name, now, note));
        DesignAreaStatus = DesignAreaStatus.FeasibilityApproved;

        Audit(actor, now, "DesignFeasibilityApproved", note);
        _domainEvents.Add(new DesignFeasibilityApprovedEvent(Id, now));
    }

    /// <summary>
    /// El admin de Diseño (o AdminTotal) corrige la clasificación del arte y los valores LAB que se
    /// pusieron al asignar o al aprobar la factibilidad. Desde 2026-11-04 el diseñador ya no la cambia.
    /// </summary>
    public void UpdateDesignClassification(Actor actor, IClock clock, ArtClassification? artClassification, bool? hasLabValues)
    {
        EnsureNotTerminal();
        if (!IsDesignAdmin(actor)) throw DesignClassificationAdminOnly();
        if (DesignFeasibility is null && ArtClassification is null)
            throw new InvalidNpiTransitionException("design-feasibility-not-approved", "La clasificación del arte se captura al asignar el diseñador o al aprobar la factibilidad de Diseño.");
        ApplyDesignClassification(artClassification, hasLabValues);

        Audit(actor, clock.UtcNow, "DesignClassificationUpdated", DescribeDesignClassification());
    }

    /// <summary>El admin de Diseño (o AdminTotal) deja o cambia las indicaciones para el diseñador; vacío las quita.</summary>
    public void SetDesignInstructions(Actor actor, IClock clock, string? text)
    {
        EnsureNotTerminal();
        if (!IsDesignAdmin(actor))
            throw new InvalidNpiTransitionException("design-instructions-admin-only", "Sólo el admin de Diseño escribe las indicaciones para el diseñador.");
        var clean = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (clean is { Length: > 2000 })
            throw new NpiValidationException("design-instructions-too-long", "Las indicaciones pueden tener máximo 2000 caracteres.");
        if (clean == DesignInstructions) return;
        DesignInstructions = clean;
        Audit(actor, clock.UtcNow, "DesignInstructionsUpdated", clean);
    }

    private static bool IsDesignAdmin(Actor actor) => actor.Role is ApplicationRoles.AdminDiseno or ApplicationRoles.AdminTotal;

    private static InvalidNpiTransitionException DesignClassificationAdminOnly() =>
        new("design-classification-admin-only", "Sólo el admin de Diseño puede cambiar la clasificación del arte y los valores LAB.");

    private bool UsesPantone => string.Equals(Capture.BasePantone, "SI", StringComparison.OrdinalIgnoreCase);

    private void ApplyDesignClassification(ArtClassification? artClassification, bool? hasLabValues)
    {
        if (artClassification is null)
            throw new NpiValidationException("art-classification-required", "Clasifica el arte (AA, A, B o C).");
        if (UsesPantone && hasLabValues is null)
            throw new NpiValidationException("lab-values-required", "El producto lleva Base Pantone: indica si el cliente tiene valores LAB.");

        ArtClassification = artClassification;
        HasLabValues = UsesPantone ? hasLabValues : null;
    }

    private string DescribeDesignClassification() =>
        HasLabValues is null ? $"Arte {ArtClassification}" : $"Arte {ArtClassification}, valores LAB: {(HasLabValues.Value ? "sí" : "no")}";

    /// <summary>
    /// Acción 9 — Ingeniería rechaza con solicitud de cambios a Comercial (no terminal).
    ///
    /// Desde 2026-11-03 también después de su factibilidad, hasta antes de cargar el PT. Ingeniería
    /// decide si el cambio afecta el PrintCard (<paramref name="redoPrintCard"/>): si sí, la versión
    /// vigente se retira (aunque ya la hubiera visto o aprobado el cliente, o ya estuviera cargada) y
    /// Diseño la rehace cuando Comercial corrija. Al reenviar, Ingeniería reconfirma su factibilidad.
    /// </summary>
    public void RequestEngineeringChanges(Actor actor, IClock clock, IBusinessCalendar calendar, string description, IReadOnlyList<string> fieldKeys, IReadOnlyDictionary<string, string>? fieldNotes = null, bool redoPrintCard = false, IReadOnlyList<Guid>? imageAttachmentIds = null)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Ingenieria);
        if (string.IsNullOrWhiteSpace(description) && fieldKeys.Count == 0)
            throw new NpiValidationException("change-request-empty", "Debe incluir una descripción o al menos un campo a modificar.");
        if (EngineeringFeasibility is not null && PtRegisteredAt is not null)
            throw new InvalidNpiTransitionException("pt-already-registered", "El PT ya se cargó en SAP: ya no se pueden pedir cambios a Comercial.");
        if (EngineeringFeasibility is not null && PendingEngineeringChangeRequest is not null)
            throw new InvalidNpiTransitionException("engineering-changes-already-pending", "Ya hay cambios de Ingeniería pendientes de que Comercial los atienda.");
        if (PendingCommercialEdit is not null)
            throw new InvalidNpiTransitionException("commercial-edit-in-progress", "Comercial está modificando el NPI: espera a que lo reenvíe.");
        var images = ValidRequestImages(imageAttachmentIds);

        var now = clock.UtcNow;
        var request = new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Ingenieria, AreaType.Comercial, ChangeRequestOrigin.Internal, description, fieldKeys, ChangeRequest.SnapshotOf(Capture));
        request.SetFieldNotes(CleanFieldNotes(fieldKeys, fieldNotes));
        request.SetImages(images);
        _changeRequests.Add(request);
        EngineeringAreaStatus = EngineeringAreaStatus.ChangesRequested;

        if (EngineeringFeasibility is not null && redoPrintCard) RetirePrintCard(actor, now, PrintCardRetiredByEngineeringReason, "PrintCardRetiredByEngineering");
        if (!DesignHasPendingWork) PauseTimer(TimerStage.Design, actor, clock, calendar, now);

        PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);
        RunCommercialTimer(actor, clock, now, "Ingeniería solicitó cambios");

        Audit(actor, now, "EngineeringRequestedChanges", description);
    }

    /// <summary>Acción 10 — Rechazo terminal por no factibilidad (sólo Ingeniería).</summary>
    public void RejectNoFeasibility(Actor actor, IClock clock, IBusinessCalendar calendar, string? note) =>
        Close(Domain.TerminalReason.NoFeasibility, actor, clock, calendar, note, null);

    /// <summary>Acción 11 — Artes Validadas (Diseño).</summary>
    public void ValidateArtwork(Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Diseno);
        // El admin de Diseño revisa el arte antes de asignar (2026-11-09): valida o rechaza artes y pide cambios.
        if (AssignedDesignUserId is null && !IsDesignAdmin(actor))
            throw new InvalidNpiTransitionException("design-not-assigned", "El NPI no tiene un diseñador asignado.");
        if (ArtesValidadasAt is not null)
            throw new InvalidNpiTransitionException("artwork-already-validated", "Las artes ya fueron validadas.");
        if (PendingArtworkRejection is not null)
            throw new InvalidNpiTransitionException("artwork-rejection-pending", "Diseño rechazó las artes y Comercial aún no sube las corregidas.");

        var now = clock.UtcNow;
        ArtesValidadasAt = now;
        ArtesValidadasByUserId = actor.UserId;
        DesignAreaStatus = DesignAreaStatus.ArtesValidadas;

        TryStartDesignTimer(actor, clock, now);

        Audit(actor, now, "ArtworkValidated", null);
    }

    // ===== Rechazos de Diseño (ida y vuelta con Comercial, igual que Ingeniería) =====
    // Todo rechazo de Diseño exige una nota: es lo que Comercial lee para saber qué corregir.

    /// <summary>
    /// Diseño rechaza con cambios a Comercial (no terminal). Comercial corrige la captura y reenvía
    /// (Submit), igual que con Ingeniería. Ingeniería sigue en paralelo: no se detiene ni se reinicia.
    ///
    /// Desde 2026-11-03 también después de aprobar su factibilidad (p. ej. al hacer el PrintCard),
    /// mientras el PrintCard no se haya enviado al cliente ni cargado: si ya había una versión en
    /// revisión, se retira (se rehará con la información corregida). Si lo que Comercial corrige toca
    /// campos de Ingeniería, al reenviar Ingeniería debe reconfirmar su factibilidad (ver Submit).
    /// </summary>
    public void RequestDesignChanges(Actor actor, IClock clock, IBusinessCalendar calendar, string description, IReadOnlyList<string> fieldKeys, IReadOnlyDictionary<string, string>? fieldNotes = null, IReadOnlyList<Guid>? imageAttachmentIds = null)
    {
        EnsureActive();
        EnsureNoClosureUnderReview(AreaType.Diseno);
        // El admin de Diseño revisa el arte antes de asignar (2026-11-09): valida o rechaza artes y pide cambios.
        if (AssignedDesignUserId is null && !IsDesignAdmin(actor))
            throw new InvalidNpiTransitionException("design-not-assigned", "El NPI no tiene un diseñador asignado.");
        if (DesignFeasibility is not null && PrintCardLoadedAt is not null)
            throw new InvalidNpiTransitionException("printcard-already-loaded", "El PrintCard ya fue cargado en sistema: ya no se pueden pedir cambios a Comercial.");
        if (DesignFeasibility is not null && PrintCardSentToClientAt is not null)
            throw new InvalidNpiTransitionException("printcard-already-sent", "El PrintCard ya se envió al cliente: los cambios ahora llegan por la respuesta del cliente.");
        if (PendingDesignChangeRequest is not null)
            throw new InvalidNpiTransitionException("design-changes-already-pending", "Ya hay cambios de Diseño pendientes de que Comercial los atienda.");
        if (PendingCommercialEdit is not null)
            throw new InvalidNpiTransitionException("commercial-edit-in-progress", "Comercial está modificando el NPI: espera a que lo reenvíe.");
        RequireRejectionNote(description);
        var images = ValidRequestImages(imageAttachmentIds);

        var now = clock.UtcNow;
        var request = new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Diseno, AreaType.Comercial, ChangeRequestOrigin.Internal, description.Trim(), fieldKeys, ChangeRequest.SnapshotOf(Capture));
        request.SetFieldNotes(CleanFieldNotes(fieldKeys, fieldNotes));
        request.SetImages(images);
        _changeRequests.Add(request);
        DesignAreaStatus = DesignAreaStatus.ChangesRequested;

        // La versión que estaba en revisión se hizo con los datos que se van a corregir: se retira.
        if (DesignFeasibility is not null && CurrentPrintCardVersion is { IsRejected: false } inReview)
        {
            inReview.Reject(actor.UserId, actor.Name, now, PrintCardRetiredByDesignReason, null);
            Audit(actor, now, "PrintCardRetiredByDesign", $"version={inReview.VersionNumber}");
        }
        if (!EngineeringHasPendingWork) PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);

        PauseTimer(TimerStage.Design, actor, clock, calendar, now);
        RunCommercialTimer(actor, clock, now, "Diseño solicitó cambios");

        Audit(actor, now, "DesignRequestedChanges", description.Trim());
    }

    /// <summary>Rechazo terminal de Diseño por no factibilidad.</summary>
    public void RejectDesignNoFeasibility(Actor actor, IClock clock, IBusinessCalendar calendar, string note) =>
        Close(Domain.TerminalReason.DesignNoFeasibility, actor, clock, calendar, note, null);

    /// <summary>
    /// Diseño rechaza las artes del cliente (no terminal): Comercial debe subir artes nuevas y
    /// avisar con <see cref="ResolveArtworkRejection"/>. Mientras tanto corre el reloj de Comercial.
    /// </summary>
    public void RejectArtwork(Actor actor, IClock clock, IBusinessCalendar calendar, string reason, Guid? evidenceAttachmentId)
    {
        EnsureActive();
        EnsureNoClosureUnderReview(AreaType.Diseno);
        // El admin de Diseño revisa el arte antes de asignar (2026-11-09): valida o rechaza artes y pide cambios.
        if (AssignedDesignUserId is null && !IsDesignAdmin(actor))
            throw new InvalidNpiTransitionException("design-not-assigned", "El NPI no tiene un diseñador asignado.");
        if (ArtesValidadasAt is not null)
            throw new InvalidNpiTransitionException("artwork-already-validated", "Las artes ya fueron validadas.");
        if (PendingArtworkRejection is not null)
            throw new InvalidNpiTransitionException("artwork-rejection-already-pending", "Las artes ya están rechazadas; falta que Comercial suba las corregidas.");
        RequireRejectionNote(reason);
        if (evidenceAttachmentId is { } evidenceId && _attachments.All(a => a.Id != evidenceId))
            throw new NpiValidationException("evidence-not-found", "La evidencia indicada no pertenece a este NPI.");

        // Cada renglón del motivo es una observación que Comercial palomea al corregir las artes.
        var items = reason.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (items.Count > 50)
            throw new NpiValidationException("design-rejection-too-many-items", "Son demasiadas observaciones en un solo rechazo (máximo 50 renglones).");
        if (items.Any(i => i.Length > 500))
            throw new NpiValidationException("design-rejection-item-too-long", "Cada renglón puede tener máximo 500 caracteres.");

        var now = clock.UtcNow;
        var request = new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Diseno, AreaType.Comercial, ChangeRequestOrigin.ArtworkRejected, reason.Trim(), [], evidenceAttachmentId: evidenceAttachmentId);
        request.SetChecklist(items.Select(text => new ChecklistItem(Guid.NewGuid(), text)));
        _changeRequests.Add(request);
        DesignAreaStatus = CurrentDesignAreaStatus();

        PauseTimer(TimerStage.Design, actor, clock, calendar, now);
        RunCommercialTimer(actor, clock, now, "Diseño rechazó las artes");

        Audit(actor, now, "ArtworkRejected", reason.Trim());
    }

    /// <summary>Comercial avisa que ya subió las artes corregidas: regresa a Diseño para validarlas.</summary>
    public void ResolveArtworkRejection(Actor actor, IClock clock, IBusinessCalendar calendar, string? comment)
    {
        EnsureActive();
        var pending = PendingArtworkRejection
            ?? throw new InvalidNpiTransitionException("no-pending-artwork-rejection", "No hay artes rechazadas pendientes.");
        if (!_attachments.Any(a => a.Context == AttachmentContext.Artwork && a.UploadedAt > pending.RequestedAt))
            throw new InvalidNpiTransitionException("new-artwork-required", "Sube las artes corregidas (archivo o link) antes de regresarlas a Diseño.");
        if (pending.Checklist.Any(i => !i.IsDone))
            throw new InvalidNpiTransitionException("artwork-observations-pending", "Palomea todas las observaciones de Diseño antes de regresarle las artes.");
        if (comment is { Length: > 2000 })
            throw new NpiValidationException("resubmit-comment-too-long", "El comentario para Diseño no puede exceder 2000 caracteres.");

        var now = clock.UtcNow;
        pending.Resolve(actor, now, comment, Capture);
        DesignAreaStatus = CurrentDesignAreaStatus();

        if (!CommercialHasPendingWork) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
        ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "ArtworkRejectionResolved", comment);
    }

    /// <summary>Comercial palomea (o despalomea) una observación del rechazo de artes.</summary>
    public void SetArtworkObservationDone(Actor actor, IClock clock, Guid itemId, bool done, string? comment = null)
    {
        EnsureActive();
        var pending = PendingArtworkRejection
            ?? throw new InvalidNpiTransitionException("no-pending-artwork-rejection", "No hay artes rechazadas pendientes.");
        if (!pending.SetChecklistItemDone(itemId, done, actor, clock.UtcNow, comment))
            throw new NpiValidationException("artwork-observation-not-found", "Esa observación no pertenece al rechazo de artes pendiente.");
    }

    /// <summary>Comentarios por campo: sólo de campos marcados, sin vacíos, máximo 500 caracteres cada uno.</summary>
    /// <summary>
    /// Imágenes de una solicitud (capturas o fotos, 2026-11-04): deben ser adjuntos de este mismo NPI y
    /// ser imagen (JPG/PNG). Con <paramref name="allowPdf"/> también PDF (los cambios del cliente suelen
    /// venir en el PrintCard anotado, 2026-11-06). Un link se adjunta aparte, en Adjuntos. Máximo RequestImages.Max.
    /// </summary>
    private IReadOnlyList<Guid> ValidRequestImages(IReadOnlyList<Guid>? ids, bool allowPdf = false)
    {
        if (ids is null || ids.Count == 0) return [];
        var distinct = ids.Distinct().ToList();
        if (distinct.Count > RequestImages.Max)
            throw new NpiValidationException("too-many-request-images", $"Adjunta máximo {RequestImages.Max} imágenes.");
        foreach (var id in distinct)
        {
            var attachment = _attachments.FirstOrDefault(a => a.Id == id)
                ?? throw new NpiValidationException("request-image-not-found", "Una de las imágenes no pertenece a este NPI.");
            var isImage = attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            var isPdf = allowPdf && string.Equals(attachment.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
            if (attachment.IsLink || !(isImage || isPdf))
                throw new NpiValidationException("request-image-not-image", allowPdf
                    ? "Sólo se pueden adjuntar imágenes (JPG o PNG) o PDF a la solicitud."
                    : "Sólo se pueden adjuntar imágenes (JPG o PNG) a la solicitud.");
        }
        return distinct;
    }

    private static Dictionary<string, string> CleanFieldNotes(IReadOnlyList<string> fieldKeys, IReadOnlyDictionary<string, string>? notes)
    {
        var clean = new Dictionary<string, string>();
        foreach (var (key, text) in notes ?? new Dictionary<string, string>())
        {
            var trimmed = text?.Trim() ?? string.Empty;
            if (trimmed.Length == 0) continue;
            if (!fieldKeys.Contains(key))
                throw new NpiValidationException("field-note-not-flagged", "Sólo se puede comentar un campo que esté marcado para cambio.");
            if (trimmed.Length > 500)
                throw new NpiValidationException("field-note-too-long", "El comentario de cada campo puede tener máximo 500 caracteres.");
            clean[key] = trimmed;
        }
        return clean;
    }

    private static void RequireRejectionNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new NpiValidationException("design-rejection-note-required", "Explica el motivo: es lo que Comercial verá para saber qué corregir.");
        if (note.Trim().Length > 2000)
            throw new NpiValidationException("design-rejection-note-too-long", "El motivo no puede exceder 2000 caracteres.");
    }

    /// <summary>Arranca o reanuda el reloj de Comercial (si ya corría, no hace nada).</summary>
    private void RunCommercialTimer(Actor actor, IClock clock, DateTimeOffset now, string note)
    {
        if (CommercialTimer.IsRunning || CommercialTimer.IsClosed) return;
        var action = CommercialTimer.HasStarted ? TimerAction.Resume : TimerAction.Start;
        CommercialTimer = CommercialTimer.StartOrResume(clock);
        AppendTimerEvent(TimerStage.Commercial, action, now, actor.UserId, note: note);
    }

    /// <summary>La última solicitud de cambios de Diseño a Comercial (sobre la captura) que aún no se atiende.</summary>
    private ChangeRequest? PendingDesignChangeRequest =>
        _changeRequests
            .Where(c => c.FromArea == AreaType.Diseno && c.ToArea == AreaType.Comercial && c.Origin == ChangeRequestOrigin.Internal && !c.IsResolved)
            .OrderByDescending(c => c.RequestedAt)
            .FirstOrDefault();

    /// <summary>El rechazo de artes que Comercial aún no atiende.</summary>
    private ChangeRequest? PendingArtworkRejection =>
        _changeRequests
            .Where(c => c.Origin == ChangeRequestOrigin.ArtworkRejected && !c.IsResolved)
            .OrderByDescending(c => c.RequestedAt)
            .FirstOrDefault();

    /// <summary>
    /// Algo espera a Comercial: cambios pedidos por Ingeniería o Diseño, artes rechazadas, una
    /// solicitud de información de Diseño o un rechazo temporal por insumos. Su reloj sólo se pausa
    /// cuando ya no queda nada de eso.
    /// </summary>
    private bool CommercialHasPendingWork =>
        _changeRequests.Any(c => c.ToArea == AreaType.Comercial && !c.IsResolved)
        || _informationRequests.Any(r => r.Type == InformationRequestType.Commercial && r.Status == InformationRequestStatus.Pending)
        || SuppliesTemporaryRejection is not null
        || PrintCardAwaitingClient
        || AwaitingSisproQuote;

    /// <summary>
    /// Campos de la captura que definen lo que se cotizó en SISPRO: si cambian, la cotización vigente
    /// deja de valer (claves camelCase de CaptureDiff, las mismas de npiFields.ts).
    /// </summary>
    private static readonly HashSet<string> QuotedStructureFields =
    [
        "tipoProducto", "formato", "ancho", "alto", "fuelleExtendido", "fuelles", "cierre",
        "piTipoPelicula", "piCalibre", "terminadoPI",
        "pplTipoPelicula", "pplCalibre", "terminadoPPL",
        "pslTipoPelicula", "pslCalibre", "terminadoPSL",
    ];

    // ===== Reconfirmación de factibilidad de Ingeniería (2026-11-03) =====
    // Cuando Comercial corrige campos que Ingeniería ya revisó después de que ésta dio factibilidad
    // (p. ej. porque Diseño pidió cambios al hacer el PrintCard), la factibilidad no se borra pero
    // Ingeniería debe reconfirmarla. Mientras tanto Diseño sigue trabajando, pero Ingeniería no aprueba
    // el PrintCard ni se envía al cliente.

    /// <summary>Motivo con el que se retira la versión del PrintCard en revisión cuando Diseño pide cambios (el front lo reconoce).</summary>
    public const string PrintCardRetiredByDesignReason = "Diseño pidió cambios a Comercial: esta versión se rehará con la información corregida.";

    /// <summary>Igual, cuando Ingeniería pide cambios que afectan el PrintCard (el front lo reconoce).</summary>
    public const string PrintCardRetiredByEngineeringReason = "Ingeniería pidió cambios a Comercial que afectan el PrintCard: esta versión se rehará con la información corregida.";

    /// <summary>
    /// Campos que NO son de Ingeniería: contacto del cliente, información complementaria (de Diseño) y
    /// observaciones. Todo lo demás (clasificación, medidas, películas, elementos, empaque…) lo revisa
    /// Ingeniería al dar factibilidad.
    /// </summary>
    private static readonly HashSet<string> NonEngineeringFields =
    [
        "numeroCotizacion", "cliente", "ejecutivoVenta", "contactoCompras", "contactoClienteDiseno",
        "basePantone", "visitaCliente", "pruebaColor", "muestrasFisicas", "especificaciones", "otros",
        "observaciones",
    ];

    public static bool IsEngineeringField(string field) => !NonEngineeringFields.Contains(field);

    /// <summary>La reconfirmación que Ingeniería aún no da.</summary>
    public ChangeRequest? PendingEngineeringReconfirmation =>
        _changeRequests
            .Where(c => c.Origin == ChangeRequestOrigin.Reconfirmation && !c.IsResolved)
            .OrderByDescending(c => c.RequestedAt)
            .FirstOrDefault();

    /// <summary>
    /// Al reenviar Comercial: si cambió campos de Ingeniería, abre la reconfirmación (o suma los campos
    /// a la que ya estaba abierta). La aprobación de Ingeniería sobre un PrintCard que aún no va al
    /// cliente se cae: se hizo con los datos de antes.
    /// </summary>
    private void RequestEngineeringReconfirmation(Actor actor, IClock clock, DateTimeOffset now, IReadOnlyList<ChangeRequest> resolved)
    {
        var changes = resolved.OrderBy(r => r.RequestedAt)
            .SelectMany(r => r.ChangesApplied)
            .Where(c => IsEngineeringField(c.Field))
            .ToList();
        if (changes.Count == 0) return;

        var origin = resolved.FirstOrDefault(r => r.Origin == ChangeRequestOrigin.CommercialEdit) is { } edit
            ? $"al modificar el NPI: {edit.Description}"
            : $"al atender cambios de {string.Join(", ", resolved.Select(r => r.FromArea == AreaType.Diseno ? "Diseño" : "Ingeniería").Distinct())}";
        if (PendingEngineeringReconfirmation is { } pending)
            pending.MergePendingChanges(changes);
        else
        {
            var request = new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Comercial, AreaType.Ingenieria, ChangeRequestOrigin.Reconfirmation,
                $"Comercial cambió campos que Ingeniería ya había revisado ({origin}).", changes.Select(c => c.Field).Distinct().ToList());
            request.MergePendingChanges(changes);
            _changeRequests.Add(request);
        }

        if (PrintCardSentToClientAt is null && CurrentPrintCardVersion is { IsRejected: false } version && version.HasApprovalFrom(ApproverRole.Engineering))
        {
            version.RemoveApproval(ApproverRole.Engineering);
            Audit(actor, now, "PrintCardEngineeringApprovalRemoved", $"version={version.VersionNumber}");
        }

        ResumeTimer(TimerStage.Engineering, actor, clock, now);
        Audit(actor, now, "EngineeringReconfirmationRequested", string.Join(", ", changes.Select(c => c.Field).Distinct()));
    }

    /// <summary>Motivo con el que se retira el PrintCard cuando Comercial modifica el NPI y dice que lo afecta (el front lo reconoce).</summary>
    public const string PrintCardRetiredByCommercialReason = "Comercial modificó el NPI y el cambio afecta el PrintCard: esta versión se rehará con la información corregida.";

    /// <summary>La modificación que Comercial abrió por su cuenta y aún no reenvía.</summary>
    public ChangeRequest? PendingCommercialEdit =>
        _changeRequests.FirstOrDefault(c => c.Origin == ChangeRequestOrigin.CommercialEdit && !c.IsResolved);

    /// <summary>
    /// Comercial modifica el NPI por su cuenta (p. ej. el cliente corrigió medidas), hasta antes de
    /// cargar el PT. Abre una solicitud a sí mismo con el motivo; edita la captura y la resuelve al
    /// reenviar (Submit), igual que cuando se los pide otra área: queda el antes → después y, si tocó
    /// campos de Ingeniería, ésta reconfirma. Si dice que afecta el PrintCard, la versión se retira.
    /// </summary>
    public void StartCommercialEdit(Actor actor, IClock clock, IBusinessCalendar calendar, string reason, bool redoPrintCard)
    {
        EnsureActive();
        EnsureNoClosureUnderReview(AreaType.Comercial);
        if (string.IsNullOrWhiteSpace(reason))
            throw new NpiValidationException("commercial-edit-reason-required", "Escribe el motivo: es lo que verán Ingeniería y Diseño.");
        if (reason.Trim().Length > 2000)
            throw new NpiValidationException("commercial-edit-reason-too-long", "El motivo no puede exceder 2000 caracteres.");
        if (PtRegisteredAt is not null)
            throw new InvalidNpiTransitionException("pt-already-registered", "El PT ya se cargó en SAP: ya no se puede modificar el NPI.");
        if (PendingCommercialEdit is not null)
            throw new InvalidNpiTransitionException("commercial-edit-in-progress", "Ya estás modificando el NPI: termina y reenvíalo.");
        if (_changeRequests.Any(c => c.ToArea == AreaType.Comercial && c.Origin == ChangeRequestOrigin.Internal && !c.IsResolved))
            throw new InvalidNpiTransitionException("changes-already-requested", "Ingeniería o Diseño ya te pidieron cambios: edita desde esa solicitud y ahí mismo corrige lo demás.");

        var now = clock.UtcNow;
        _changeRequests.Add(new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Comercial, AreaType.Comercial, ChangeRequestOrigin.CommercialEdit,
            reason.Trim(), [], ChangeRequest.SnapshotOf(Capture)));
        if (redoPrintCard) RetirePrintCard(actor, now, PrintCardRetiredByCommercialReason, "PrintCardRetiredByCommercial");

        // Nadie trabaja con datos que están por cambiar.
        if (!DesignHasPendingWork) PauseTimer(TimerStage.Design, actor, clock, calendar, now);
        if (!EngineeringHasPendingWork) PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);
        RunCommercialTimer(actor, clock, now, "Comercial modifica el NPI");

        Audit(actor, now, "CommercialEditStarted", reason.Trim());
    }

    /// <summary>
    /// Retira la versión vigente del PrintCard porque se hizo con datos que van a cambiar. Si ya estaba
    /// cargada en sistema se descarga y el reloj de Diseño se reabre con su acumulado (deshacer la
    /// restaura a partir del evento PrintCardUnloadedByEngineering, que guarda cómo estaba).
    /// </summary>
    private void RetirePrintCard(Actor actor, DateTimeOffset now, string reason, string auditAction)
    {
        if (CurrentPrintCardVersion is not { IsRejected: false } current) return;
        current.Reject(actor.UserId, actor.Name, now, reason, null);
        Audit(actor, now, auditAction, $"version={current.VersionNumber}");
        if (PrintCardLoadedAt is { } loadedAt)
        {
            Audit(actor, now, "PrintCardUnloadedByEngineering", $"loadedAt={loadedAt:O};by={PrintCardLoadedByUserId};version={PrintCardLoadedVersionId}");
            PrintCardLoadedAt = null;
            PrintCardLoadedByUserId = null;
            PrintCardLoadedVersionId = null;
            DesignTimer = DesignTimer.Reopen();
        }
        DesignAreaStatus = CurrentDesignAreaStatus();
    }

    /// <summary>
    /// AdminTotal corrige qué área pidió una solicitud de cambios que Comercial aún no atiende (p. ej. se
    /// registró desde la tarjeta de Ingeniería pero la pidió Diseño). No cambia lo que Comercial debe
    /// corregir: sólo a quién se le atribuye, el estado de cada área y sus relojes.
    /// </summary>
    public void CorrectChangeRequestArea(Actor actor, IClock clock, IBusinessCalendar calendar, Guid requestId, AreaType area, string reason)
    {
        EnsureActive();
        if (string.IsNullOrWhiteSpace(reason))
            throw new NpiValidationException("correction-reason-required", "Escribe el motivo de la corrección.");
        if (reason.Trim().Length > 2000)
            throw new NpiValidationException("correction-reason-too-long", "El motivo no puede exceder 2000 caracteres.");
        if (area is not (AreaType.Ingenieria or AreaType.Diseno))
            throw new NpiValidationException("correction-area-invalid", "Sólo Ingeniería o Diseño piden cambios a Comercial.");
        var request = _changeRequests.FirstOrDefault(c => c.Id == requestId)
            ?? throw new InvalidNpiTransitionException("change-request-not-found", "Esa solicitud no pertenece al NPI.");
        if (request.Origin != ChangeRequestOrigin.Internal || request.ToArea != AreaType.Comercial || request.FromArea is not (AreaType.Ingenieria or AreaType.Diseno))
            throw new InvalidNpiTransitionException("change-request-not-correctable", "Sólo se corrige el área de una solicitud de cambios de Ingeniería o Diseño a Comercial.");
        if (request.IsResolved)
            throw new InvalidNpiTransitionException("change-request-already-resolved", "Comercial ya la atendió: ya no se puede corregir.");
        if (request.FromArea == area)
            throw new InvalidNpiTransitionException("change-request-same-area", "La solicitud ya está a nombre de esa área.");
        if (area == AreaType.Diseno && PendingDesignChangeRequest is not null)
            throw new InvalidNpiTransitionException("design-changes-already-pending", "Diseño ya tiene otra solicitud pendiente.");
        if (area == AreaType.Ingenieria && PendingEngineeringChangeRequest is not null)
            throw new InvalidNpiTransitionException("engineering-changes-already-pending", "Ingeniería ya tiene otra solicitud pendiente.");

        var now = clock.UtcNow;
        var from = request.FromArea;
        request.CorrectFromArea(area);
        // Submit sabe que Ingeniería espera por su estado "Cambios solicitados": se fija a mano al pasarle la solicitud.
        EngineeringAreaStatus = area == AreaType.Ingenieria ? EngineeringAreaStatus.ChangesRequested : CurrentEngineeringAreaStatus();
        DesignAreaStatus = CurrentDesignAreaStatus();
        SyncAreaTimers(actor, clock, calendar, now);

        Audit(actor, now, "ChangeRequestAreaCorrected", $"{from}->{area}: {reason.Trim()}");
    }

    /// <summary>Ingeniería revisa los cambios de Comercial y confirma que sigue siendo factible.</summary>
    public void ReconfirmEngineeringFeasibility(Actor actor, IClock clock, IBusinessCalendar calendar, string? note)
    {
        EnsureActive();
        EnsureNoClosureUnderReview(AreaType.Ingenieria);
        var pending = PendingEngineeringReconfirmation
            ?? throw new InvalidNpiTransitionException("no-pending-reconfirmation", "No hay cambios pendientes de reconfirmar.");
        if (PendingEngineeringChangeRequest is not null)
            throw new InvalidNpiTransitionException("engineering-changes-pending", "Comercial aún no atiende los cambios que pidió Ingeniería.");
        if (note is { Length: > 2000 })
            throw new NpiValidationException("reconfirmation-note-too-long", "La nota no puede exceder 2000 caracteres.");

        var now = clock.UtcNow;
        pending.Acknowledge(actor, now, note);
        EngineeringAreaStatus = CurrentEngineeringAreaStatus();
        if (!EngineeringHasPendingWork) PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);

        Audit(actor, now, "EngineeringFeasibilityReconfirmed", note);
    }

    /// <summary>
    /// Ingeniería ya validó la estructura y falta que el cliente acepte el precio. Sólo cuenta antes del
    /// primer PrintCard: los NPIs que ya iban en PrintCard cuando se agregó la cotización no se detienen.
    /// Con la etapa apagada (SisproQuoteGate) nunca se espera.
    /// </summary>
    private bool AwaitingSisproQuote =>
        SisproQuoteGate.Enabled
        && LifecycleStatus == Domain.LifecycleStatus.Active
        && EngineeringFeasibility is not null
        && !SisproQuoteAccepted
        && _printCardVersions.Count == 0;

    /// <summary>
    /// Los cambios que pidió el cliente sobre la versión vigente y que Diseño aún no entrega en una
    /// nueva. Antes estas solicitudes nunca se cerraban: las anteriores a la versión vigente ya
    /// fueron atendidas por ella aunque sigan sin ResolvedAt.
    /// </summary>
    private ChangeRequest? PendingClientChangeRequest =>
        _changeRequests
            .Where(c => c.Origin == ChangeRequestOrigin.ClientReported && !c.IsResolved
                && (CurrentPrintCardVersion is not { } current || c.RequestedAt >= current.UploadedAt))
            .OrderByDescending(c => c.RequestedAt)
            .FirstOrDefault();

    /// <summary>La versión vigente espera la revisión de Ingeniería.</summary>
    private bool PrintCardAwaitingEngineering =>
        PrintCardLoadedAt is null
        && CurrentPrintCardVersion is { IsRejected: false } v
        && !v.HasApprovalFrom(ApproverRole.Engineering)
        && PendingClientChangeRequest is null;

    /// <summary>Ingeniería ya aprobó la versión vigente y falta la respuesta del cliente (la gestiona Comercial).</summary>
    private bool PrintCardAwaitingClient =>
        PrintCardLoadedAt is null
        && CurrentPrintCardVersion is { IsRejected: false } v
        && v.HasApprovalFrom(ApproverRole.Engineering)
        && !v.HasApprovalFrom(ApproverRole.Client)
        && PendingClientChangeRequest is null;

    /// <summary>
    /// Estado de Diseño a partir de los hechos, para cuando se resuelve algo que lo tenía detenido
    /// (cambios, artes rechazadas, solicitudes de información). Antes se quedaba, p. ej., en
    /// "Esperando Información" aunque la información ya hubiera llegado.
    /// </summary>
    private DesignAreaStatus CurrentDesignAreaStatus()
    {
        if (PendingDesignChangeRequest is not null) return DesignAreaStatus.ChangesRequested;
        if (PendingArtworkRejection is not null) return DesignAreaStatus.ArtworkRejected;
        if (_informationRequests.Any(r => r.Status == InformationRequestStatus.Pending)) return DesignAreaStatus.AwaitingInformation;
        if (PrintCardLoadedAt is not null) return DesignAreaStatus.PrintCardLoaded;
        if (PendingClientChangeRequest is not null || CurrentPrintCardVersion is { IsRejected: true }) return DesignAreaStatus.PrintCardChangesRequested;
        if (_printCardVersions.Count > 0) return DesignAreaStatus.PrintCardInApproval;
        if (ArtesValidadasAt is not null) return DesignAreaStatus.ArtesValidadas;
        if (DesignFeasibility is not null) return DesignAreaStatus.FeasibilityApproved;
        return AssignedDesignUserId is not null ? DesignAreaStatus.Assigned : DesignAreaStatus.Requested;
    }

    /// <summary>
    /// Reanuda Diseño si ya había arrancado; si no, sólo lo arranca cuando se cumplen sus
    /// condiciones (TryStartDesignTimer). Nunca lo arranca antes de tiempo ni reabre uno cerrado.
    /// </summary>
    private void ResumeDesignTimer(Actor actor, IClock clock, DateTimeOffset now)
    {
        if (!DesignTimer.HasStarted) { TryStartDesignTimer(actor, clock, now); return; }
        if (DesignTimer.IsRunning || DesignTimer.IsClosed) return;
        DesignTimer = DesignTimer.StartOrResume(clock);
        AppendTimerEvent(TimerStage.Design, TimerAction.Resume, now, actor.UserId);
    }

    /// <summary>La última solicitud de cambios de Ingeniería a Comercial que aún no se atiende.</summary>
    private ChangeRequest? PendingEngineeringChangeRequest =>
        _changeRequests
            .Where(c => c.FromArea == AreaType.Ingenieria && c.ToArea == AreaType.Comercial && !c.IsResolved)
            .OrderByDescending(c => c.RequestedAt)
            .FirstOrDefault();

    /// <summary>
    /// Tramos en que la pelota está en la cancha de Ingeniería: antes de su factibilidad, revisando
    /// la versión vigente del PrintCard, entre
    /// el PrintCard cargado y el PT, y entre el Pedido SAP y la ET en SisPro. Fuera de ellos su
    /// reloj va en pausa (espera a Diseño, al cliente o a Comercial).
    /// </summary>
    private bool EngineeringHasPendingWork =>
        EngineeringFeasibility is null
        || _informationRequests.Any(r => r.Type == InformationRequestType.Engineering && r.Status == InformationRequestStatus.Pending)
        || (PendingEngineeringReconfirmation is not null && PendingEngineeringChangeRequest is null)
        || (PrintCardAwaitingEngineering && PendingEngineeringChangeRequest is null)
        || (PrintCardLoadedAt is not null && PtRegisteredAt is null && PendingEngineeringChangeRequest is null && PendingCommercialEdit is null)
        || (PedidoSapRegisteredAt is not null && EtLoadedInSisproAt is null);

    /// <summary>Reanuda un reloj que ya arrancó y está en pausa (nunca arranca uno nuevo ni reabre uno cerrado).</summary>
    private void ResumeTimer(TimerStage stage, Actor actor, IClock clock, DateTimeOffset now)
    {
        var timer = stage switch
        {
            TimerStage.Commercial => CommercialTimer,
            TimerStage.Design => DesignTimer,
            TimerStage.Engineering => EngineeringTimer,
            _ => GlobalTimer,
        };
        if (!timer.HasStarted || timer.IsRunning || timer.IsClosed) return;
        var resumed = timer.StartOrResume(clock);
        switch (stage)
        {
            case TimerStage.Commercial: CommercialTimer = resumed; break;
            case TimerStage.Design: DesignTimer = resumed; break;
            case TimerStage.Engineering: EngineeringTimer = resumed; break;
            default: GlobalTimer = resumed; break;
        }
        AppendTimerEvent(stage, TimerAction.Resume, now, actor.UserId);
    }

    /// <summary>Pausa un reloj y registra el evento sólo si de verdad estaba corriendo (la Cronología no muestra pausas fantasma).</summary>
    private void PauseTimer(TimerStage stage, Actor actor, IClock clock, IBusinessCalendar calendar, DateTimeOffset now)
    {
        var timer = stage switch
        {
            TimerStage.Commercial => CommercialTimer,
            TimerStage.Design => DesignTimer,
            TimerStage.Engineering => EngineeringTimer,
            _ => GlobalTimer,
        };
        if (!timer.IsRunning) return;
        var paused = timer.Pause(clock, calendar);
        switch (stage)
        {
            case TimerStage.Commercial: CommercialTimer = paused; break;
            case TimerStage.Design: DesignTimer = paused; break;
            case TimerStage.Engineering: EngineeringTimer = paused; break;
            default: GlobalTimer = paused; break;
        }
        AppendTimerEvent(stage, TimerAction.Pause, now, actor.UserId);
    }

    /// <summary>
    /// Arranca DesignTimer si ya se cumplen sus condiciones (invariante 6): Ing. factible, Artes Validadas
    /// y, desde la cotización en SISPRO, el precio aceptado por el cliente (antes no hay PrintCard que hacer).
    /// </summary>
    private void TryStartDesignTimer(Actor actor, IClock clock, DateTimeOffset now)
    {
        if (EngineeringFeasibility is null || ArtesValidadasAt is null) return;
        if (AwaitingSisproQuote) return;
        if (DesignTimer.HasStarted) return;

        DesignTimer = DesignTimer.StartOrResume(clock);
        AppendTimerEvent(TimerStage.Design, TimerAction.Start, now, actor.UserId);
    }

    /// <summary>
    /// Con la etapa de cotización apagada, suelta a un NPI que se quedó esperándola: pausa el reloj de
    /// Comercial si sólo corría por ella y arranca el de Diseño si ya le tocaba. Lo hace a nombre de
    /// quien aprobó la factibilidad de Ingeniería (el último en moverlo hacia la cotización).
    /// Devuelve false si no había nada que soltar; idempotente.
    /// </summary>
    public bool ReleaseFromSisproQuoteWait(IClock clock, IBusinessCalendar calendar)
    {
        if (SisproQuoteGate.Enabled || LifecycleStatus != Domain.LifecycleStatus.Active) return false;
        if (EngineeringFeasibility is not { } feasibility || _printCardVersions.Count > 0) return false;

        var commercialStuck = CommercialTimer.IsRunning && !CommercialHasPendingWork;
        var designStuck = !DesignTimer.HasStarted && ArtesValidadasAt is not null;
        if (!commercialStuck && !designStuck) return false;

        var actor = new Actor(feasibility.ApprovedByUserId, "Ingenieria", feasibility.ApprovedByName);
        var now = clock.UtcNow;
        if (commercialStuck) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
        TryStartDesignTimer(actor, clock, now);
        Audit(actor, now, "SisproQuoteWaitReleased", "Etapa de cotización SISPRO apagada: el NPI sigue sin cotización ligada.");
        return true;
    }

    // ===== Cotización en SISPRO =====
    // Se cotiza en SISPRO (cotizador oficial) sobre la estructura que Ingeniería ya validó; aquí sólo
    // se liga el número y se registra la respuesta del cliente al precio. Mientras el cliente no
    // acepta, corre el reloj de Comercial y no arranca el PrintCard.

    /// <summary>
    /// Comercial liga una cotización de SISPRO (Application ya la leyó y comparó contra la captura).
    /// Si la vigente seguía esperando al cliente, queda reemplazada por ésta.
    /// </summary>
    public SisproQuoteLink LinkSisproQuote(Actor actor, IClock clock, string numeroCotizacion, string estadoSispro, string snapshotJson, IReadOnlyList<string> structureWarnings)
    {
        EnsureActive();
        EnsureNoClosureUnderReview(AreaType.Comercial);
        if (EngineeringFeasibility is null)
            throw new InvalidNpiTransitionException("sispro-quote-before-feasibility", "Liga la cotización cuando Ingeniería apruebe la factibilidad: así se cotiza la estructura que sí se puede fabricar.");
        if (SisproQuoteAccepted)
            throw new InvalidNpiTransitionException("sispro-quote-already-accepted", "El cliente ya aceptó el precio de la cotización vigente.");
        if (string.IsNullOrWhiteSpace(numeroCotizacion))
            throw new NpiValidationException("sispro-quote-number-required", "Indica el número de cotización de SISPRO.");
        if (numeroCotizacion.Trim().Length > 30)
            throw new NpiValidationException("sispro-quote-number-too-long", "El número de cotización no puede exceder 30 caracteres.");

        var now = clock.UtcNow;
        if (CurrentSisproQuote is { Status: SisproQuoteStatus.AwaitingClient } previous) previous.Supersede();

        var link = new SisproQuoteLink(Guid.NewGuid(), _sisproQuotes.Count + 1, numeroCotizacion.Trim(), estadoSispro, snapshotJson,
            structureWarnings, actor.UserId, actor.Name, now);
        _sisproQuotes.Add(link);

        Audit(actor, now, "SisproQuoteLinked", structureWarnings.Count == 0
            ? $"cotizacion={link.NumeroCotizacion}"
            : $"cotizacion={link.NumeroCotizacion}; {structureWarnings.Count} diferencia(s) de estructura");
        return link;
    }

    /// <summary>El cliente acepta el precio: con eso arranca el PrintCard (Diseño).</summary>
    public void AcceptSisproQuote(Actor actor, IClock clock, IBusinessCalendar calendar, string? note, Guid? evidenceAttachmentId)
    {
        var quote = RequireQuoteAwaitingClient();
        EnsureNoClosureUnderReview(AreaType.Comercial);
        if (note is { Length: > 2000 })
            throw new NpiValidationException("sispro-quote-note-too-long", "La nota no puede exceder 2000 caracteres.");
        if (evidenceAttachmentId is { } evidenceId && _attachments.All(a => a.Id != evidenceId))
            throw new NpiValidationException("evidence-not-found", "La evidencia indicada no pertenece a este NPI.");

        var now = clock.UtcNow;
        quote.Respond(SisproQuoteStatus.Accepted, actor, now, note?.Trim(), evidenceAttachmentId);

        if (!CommercialHasPendingWork) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
        TryStartDesignTimer(actor, clock, now);

        Audit(actor, now, "SisproQuoteAccepted", $"cotizacion={quote.NumeroCotizacion}");
    }

    /// <summary>El cliente pide ajustar el precio (no terminal): Comercial recotiza en SISPRO y liga la nueva.</summary>
    public void RequestSisproQuoteAdjustment(Actor actor, IClock clock, string note)
    {
        var quote = RequireQuoteAwaitingClient();
        EnsureNoClosureUnderReview(AreaType.Comercial);
        if (string.IsNullOrWhiteSpace(note))
            throw new NpiValidationException("sispro-quote-adjustment-note-required", "Anota qué pidió el cliente (precio objetivo, otra cantidad...).");
        if (note.Trim().Length > 2000)
            throw new NpiValidationException("sispro-quote-note-too-long", "La nota no puede exceder 2000 caracteres.");

        var now = clock.UtcNow;
        quote.Respond(SisproQuoteStatus.AdjustmentRequested, actor, now, note.Trim(), null);
        Audit(actor, now, "SisproQuoteAdjustmentRequested", $"cotizacion={quote.NumeroCotizacion}; {note.Trim()}");
    }

    /// <summary>Rechazo terminal por precio: el cliente no aceptó la cotización.</summary>
    public void RejectByPrice(Actor actor, IClock clock, IBusinessCalendar calendar, string comment, Guid? evidenceAttachmentId) =>
        Close(Domain.TerminalReason.PriceRejection, actor, clock, calendar, comment, evidenceAttachmentId);

    private SisproQuoteLink RequireQuoteAwaitingClient()
    {
        EnsureActive();
        var quote = CurrentSisproQuote
            ?? throw new InvalidNpiTransitionException("no-sispro-quote", "Aún no hay una cotización de SISPRO ligada.");
        if (quote.Status != SisproQuoteStatus.AwaitingClient)
            throw new InvalidNpiTransitionException("sispro-quote-not-awaiting-client", "La cotización vigente no está esperando la respuesta del cliente.");
        return quote;
    }

    // ===== PrintCard =====
    // Orden: Diseño genera la versión (queda aprobada por Diseño) → Ingeniería la aprueba o la
    // rechaza → con la aprobación de Ingeniería, Comercial la envía al cliente → el cliente aprueba
    // (con evidencia), pide cambios (checklist que Diseño palomea) o la rechaza → Diseño la carga.
    // Relojes: mientras revisa Ingeniería corre Ingeniería; mientras revisa el cliente corre
    // Comercial; cuando hay que rehacer la versión o cargarla corre Diseño.

    /// <summary>Acción 12 — Crear versión de PrintCard. Exige las 4 condiciones de la decisión 2 y el archivo del PrintCard.</summary>
    public void CreatePrintCardVersion(Actor actor, IClock clock, IBusinessCalendar calendar, string code, Guid? attachmentId, string? note, IReadOnlyList<string>? commercialChecklist = null)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Diseno);
        if (string.IsNullOrWhiteSpace(code))
            throw new NpiValidationException("printcard-code-required", "El código de PrintCard es obligatorio.");
        if (attachmentId is null)
            throw new NpiValidationException("printcard-file-required", "Adjunta el archivo o link del PrintCard: nadie puede aprobar algo que no ve.");
        if (_attachments.All(a => a.Id != attachmentId))
            throw new NpiValidationException("evidence-not-found", "El archivo del PrintCard no pertenece a este NPI.");
        if (AssignedDesignUserId is null)
            throw new InvalidNpiTransitionException("design-not-assigned", "PrintCard requiere un diseñador asignado.");
        if (EngineeringFeasibility is null)
            throw new InvalidNpiTransitionException("engineering-feasibility-missing", "PrintCard requiere Factibilidad de Ingeniería aprobada.");
        if (DesignFeasibility is null)
            throw new InvalidNpiTransitionException("design-feasibility-missing", "PrintCard requiere Factibilidad de Diseño aprobada.");
        if (ArtesValidadasAt is null)
            throw new InvalidNpiTransitionException("artwork-not-validated", "PrintCard requiere Artes Validadas.");
        if (AwaitingSisproQuote)
            throw new InvalidNpiTransitionException("sispro-quote-not-accepted", "El PrintCard arranca cuando el cliente acepta el precio: Comercial debe ligar la cotización de SISPRO y registrar la aceptación.");
        if (PrintCardLoadedAt is not null)
            throw new InvalidNpiTransitionException("printcard-already-loaded", "El PrintCard ya fue cargado en sistema.");
        if (PendingEngineeringChangeRequest is not null || PendingDesignChangeRequest is not null || PendingCommercialEdit is not null)
            throw new InvalidNpiTransitionException("commercial-changes-pending", "Comercial aún está corrigiendo datos del NPI: haz la versión cuando reenvíe.");
        if (CurrentPrintCardVersion is { } current && !current.IsRejected && PendingClientChangeRequest is null)
            throw new InvalidNpiTransitionException("printcard-version-in-review", "La versión vigente sigue en revisión: sólo se genera otra si Ingeniería la rechaza o el cliente pide cambios.");
        var clientChanges = PendingClientChangeRequest;
        if (clientChanges is not null && clientChanges.Checklist.Any(i => !i.IsDone))
            throw new InvalidNpiTransitionException("client-changes-pending", "Palomea todos los cambios que pidió el cliente antes de enviar la nueva versión.");
        var checklist = (commercialChecklist ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        if (checklist.Count > 20)
            throw new NpiValidationException("printcard-checklist-too-long", "Máximo 20 puntos para Comercial.");
        if (checklist.Any(t => t.Length > 500))
            throw new NpiValidationException("printcard-checklist-item-too-long", "Cada punto para Comercial puede tener máximo 500 caracteres.");

        var now = clock.UtcNow;
        var version = new PrintCardVersion(Guid.NewGuid(), _printCardVersions.Count + 1, code.Trim(), attachmentId, actor.UserId, actor.Name, now, note);
        // Quien la genera ya está de acuerdo con ella: la aprobación de Diseño queda registrada sola.
        version.AddApproval(Guid.NewGuid(), ApproverRole.Design, actor.UserId, actor.Name, now, null, null);
        if (checklist.Count > 0) version.SetCommercialChecklist(checklist.Select(text => new ChecklistItem(Guid.NewGuid(), text)));
        _printCardVersions.Add(version);
        clientChanges?.Resolve(actor, now, $"Atendido en la versión {version.VersionNumber}", Capture);
        DesignAreaStatus = DesignAreaStatus.PrintCardInApproval;
        // Cada versión nueva necesita su propio envío al cliente — el envío de una versión
        // anterior no vale para ésta (evita aprobar "en nombre del cliente" algo que nunca vio).
        PrintCardSentToClientAt = null;
        PrintCardSentToClientByUserId = null;

        // Ahora le toca revisar a Ingeniería.
        PauseTimer(TimerStage.Design, actor, clock, calendar, now);
        ResumeTimer(TimerStage.Engineering, actor, clock, now);

        Audit(actor, now, "PrintCardVersionCreated", $"version={version.VersionNumber}");
    }

    /// <summary>Acción 13 — Aprobar la versión vigente de PrintCard bajo un rol. "Client" siempre lo ejecuta Comercial (decisión 8).</summary>
    /// <summary>
    /// Diseño reemplaza el archivo de la versión vigente del PrintCard (subió el equivocado, 2026-11-09).
    /// Sólo mientras nadie la ha revisado: ni Ingeniería la aprobó o rechazó, ni salió al cliente, ni se
    /// cargó. Sigue siendo la misma versión; el archivo anterior se queda en el servidor y en la bitácora.
    /// Ya revisada, cualquier cambio va en una versión nueva, como siempre.
    /// </summary>
    public void ReplacePrintCardFile(Actor actor, IClock clock, Guid attachmentId)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Diseno);
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No hay un PrintCard que reemplazar.");
        if (version.IsRejected)
            throw new InvalidNpiTransitionException("printcard-version-rejected", "Esa versión ya se rechazó: sube una versión nueva.");
        if (version.Approvals.Any(a => a.Role != ApproverRole.Design) || PrintCardLoadedAt is not null)
            throw new InvalidNpiTransitionException("printcard-already-reviewed", $"La v{version.VersionNumber} ya la revisó Ingeniería: los cambios van en una versión nueva.");
        var file = _attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new NpiValidationException("evidence-not-found", "El archivo del PrintCard no pertenece a este NPI.");
        if (version.AttachmentId == attachmentId) return;

        var previous = _attachments.FirstOrDefault(a => a.Id == version.AttachmentId);
        version.ReplaceFile(attachmentId);
        Audit(actor, clock.UtcNow, "PrintCardFileReplaced", $"v{version.VersionNumber}: {previous?.FileName ?? "sin archivo"} → {file.FileName}");
    }

    public void ApprovePrintCard(Actor actor, IClock clock, IBusinessCalendar calendar, ApproverRole role, string? note, Guid? evidenceAttachmentId)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(role switch { ApproverRole.Design => AreaType.Diseno, ApproverRole.Engineering => AreaType.Ingenieria, _ => AreaType.Comercial });
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard para aprobar.");
        if (version.IsRejected)
            throw new InvalidNpiTransitionException("printcard-version-rejected", "Ingeniería rechazó esta versión: Diseño debe generar una nueva.");
        if (PendingClientChangeRequest is not null)
            throw new InvalidNpiTransitionException("client-changes-pending", "El cliente pidió cambios sobre esta versión: Diseño debe generar una nueva.");
        if (version.HasApprovalFrom(role))
            throw new InvalidNpiTransitionException("printcard-already-approved-by-role", $"La versión vigente ya fue aprobada por el rol {role}.");
        if (role == ApproverRole.Engineering && PendingEngineeringReconfirmation is not null)
            throw new InvalidNpiTransitionException("engineering-reconfirmation-pending", "Primero reconfirma la factibilidad con los cambios que hizo Comercial.");
        if (role == ApproverRole.Client && !version.HasApprovalFrom(ApproverRole.Engineering))
            throw new InvalidNpiTransitionException("printcard-engineering-approval-missing", "Ingeniería debe aprobar el PrintCard antes que el cliente.");
        if (role == ApproverRole.Client && PrintCardSentToClientAt is null)
            throw new InvalidNpiTransitionException("printcard-not-sent-to-client", "Debes enviar el PrintCard al cliente (\"Enviar a Cliente\") antes de registrar su aprobación.");
        if (role == ApproverRole.Client && evidenceAttachmentId is null)
            throw new NpiValidationException("client-approval-evidence-required", "La aprobación en nombre del Cliente requiere evidencia obligatoria.");
        if (role == ApproverRole.Client && version.CommercialChecklist.Any(i => !i.IsDone))
            throw new InvalidNpiTransitionException("printcard-observations-pending", "Palomea los puntos que Diseño pidió revisar con el cliente antes de registrar su aprobación.");
        if (evidenceAttachmentId is { } evidenceId && _attachments.All(a => a.Id != evidenceId))
            throw new NpiValidationException("evidence-not-found", "La evidencia indicada no pertenece a este NPI.");

        var now = clock.UtcNow;
        version.AddApproval(Guid.NewGuid(), role, actor.UserId, actor.Name, now, note, evidenceAttachmentId);
        Audit(actor, now, "PrintCardApproved", $"role={role}");

        if (role == ApproverRole.Engineering)
        {
            // Ingeniería ya revisó: ahora Comercial lo lleva al cliente.
            PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);
            RunCommercialTimer(actor, clock, now, "PrintCard aprobado por Ingeniería: enviarlo al cliente");
        }
        else if (role == ApproverRole.Client)
        {
            // Con el visto bueno del cliente, a Diseño le toca cargarlo en sistema.
            if (!CommercialHasPendingWork) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
            ResumeDesignTimer(actor, clock, now);
        }

        if (version.IsFullyApproved)
        {
            Audit(actor, now, "PrintCardFullyApproved", null);
            _domainEvents.Add(new PrintCardFullyApprovedEvent(Id, version.Id, now));
        }
    }

    /// <summary>Comercial palomea (o despalomea) un punto que Diseño le pidió revisar con el cliente en la versión vigente.</summary>
    public void SetPrintCardObservationDone(Actor actor, IClock clock, Guid itemId, bool done, string? comment = null)
    {
        EnsureActive();
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard.");
        if (version.IsRejected || version.HasApprovalFrom(ApproverRole.Client))
            throw new InvalidNpiTransitionException("printcard-observations-closed", "Esta versión ya no está con el cliente.");
        if (!version.SetCommercialChecklistItemDone(itemId, done, actor, clock.UtcNow, comment))
            throw new NpiValidationException("printcard-observation-not-found", "Ese punto no pertenece a la versión vigente del PrintCard.");
    }

    /// <summary>Ingeniería rechaza la versión vigente con motivo: Diseño debe generar una nueva.</summary>
    public void RejectPrintCard(Actor actor, IClock clock, IBusinessCalendar calendar, string reason, Guid? evidenceAttachmentId)
    {
        EnsureNotTerminal();
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard para rechazar.");
        if (version.IsRejected)
            throw new InvalidNpiTransitionException("printcard-version-rejected", "Esta versión ya fue rechazada.");
        if (version.HasApprovalFrom(ApproverRole.Engineering))
            throw new InvalidNpiTransitionException("printcard-already-approved-by-role", "Ingeniería ya aprobó esta versión.");
        if (PrintCardLoadedAt is not null)
            throw new InvalidNpiTransitionException("printcard-already-loaded", "El PrintCard ya fue cargado en sistema.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new NpiValidationException("printcard-rejection-reason-required", "Explica qué está mal: es lo que Diseño verá para corregir el PrintCard.");
        if (reason.Trim().Length > 2000)
            throw new NpiValidationException("printcard-rejection-reason-too-long", "El motivo no puede exceder 2000 caracteres.");
        if (evidenceAttachmentId is { } evidenceId && _attachments.All(a => a.Id != evidenceId))
            throw new NpiValidationException("evidence-not-found", "La evidencia indicada no pertenece a este NPI.");

        var now = clock.UtcNow;
        version.Reject(actor.UserId, actor.Name, now, reason.Trim(), evidenceAttachmentId);
        DesignAreaStatus = DesignAreaStatus.PrintCardChangesRequested;

        PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);
        ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "PrintCardRejectedByEngineering", $"version={version.VersionNumber}; {reason.Trim()}");
    }

    /// <summary>Acción 14 — Registrar el envío del PrintCard vigente al cliente (informativo). Sólo después de que Ingeniería lo aprobó.</summary>
    public void SendPrintCardToClient(Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Comercial);
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard para enviar.");
        if (version.IsRejected)
            throw new InvalidNpiTransitionException("printcard-version-rejected", "Ingeniería rechazó esta versión: Diseño debe generar una nueva.");
        if (!version.HasApprovalFrom(ApproverRole.Engineering))
            throw new InvalidNpiTransitionException("printcard-engineering-approval-missing", "Ingeniería debe aprobar el PrintCard antes de enviarlo al cliente.");
        if (PendingClientChangeRequest is not null)
            throw new InvalidNpiTransitionException("client-changes-pending", "El cliente pidió cambios sobre esta versión: Diseño debe generar una nueva.");
        if (PendingEngineeringReconfirmation is not null)
            throw new InvalidNpiTransitionException("engineering-reconfirmation-pending", "Ingeniería debe reconfirmar la factibilidad con los cambios de Comercial antes de enviar el PrintCard al cliente.");

        var now = clock.UtcNow;
        PrintCardSentToClientAt = now;
        PrintCardSentToClientByUserId = actor.UserId;
        Audit(actor, now, "PrintCardSentToClient", null);
    }

    /// <summary>
    /// Acción 15 — Cliente solicita cambios (registrado por Comercial, no terminal). Cada cambio va
    /// en su propio renglón: Diseño los palomea y no puede enviar la nueva versión hasta tenerlos todos.
    /// </summary>
    public void RequestClientChanges(Actor actor, IClock clock, IBusinessCalendar calendar, string? description, IReadOnlyList<string> items, IReadOnlyList<Guid>? attachmentIds = null)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Comercial);
        var cleanItems = (items ?? []).Select(i => i?.Trim() ?? "").Where(i => i.Length > 0).ToList();
        if (cleanItems.Count == 0)
            throw new NpiValidationException("change-request-empty", "Agrega al menos un cambio que pidió el cliente.");
        if (cleanItems.Count > 50)
            throw new NpiValidationException("change-request-too-many-items", "Son demasiados cambios en una sola solicitud (máximo 50).");
        if (cleanItems.Any(i => i.Length > 500))
            throw new NpiValidationException("change-request-item-too-long", "Cada cambio puede tener máximo 500 caracteres.");
        if (description is { Length: > 4000 })
            throw new NpiValidationException("change-request-too-long", "El comentario no puede exceder 4000 caracteres.");
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard vigente.");
        if (version.IsFullyApproved)
            throw new InvalidNpiTransitionException("printcard-already-fully-approved", "La versión vigente ya está totalmente aprobada.");
        if (PrintCardSentToClientAt is null)
            throw new InvalidNpiTransitionException("printcard-not-sent-to-client", "Primero registra «Enviar a Cliente»: el cliente no puede pedir cambios de algo que no ha visto.");
        if (PendingClientChangeRequest is not null)
            throw new InvalidNpiTransitionException("client-changes-already-pending", "Ya hay cambios del cliente pendientes de que Diseño los atienda.");
        // Capturas, fotos o el PDF anotado que mandó el cliente (2026-11-06).
        var files = ValidRequestImages(attachmentIds, allowPdf: true);

        var now = clock.UtcNow;
        var request = new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Comercial, AreaType.Diseno, ChangeRequestOrigin.ClientReported, description?.Trim() ?? string.Empty, []);
        request.SetChecklist(cleanItems.Select(text => new ChecklistItem(Guid.NewGuid(), text)));
        request.SetImages(files);
        _changeRequests.Add(request);
        DesignAreaStatus = DesignAreaStatus.PrintCardChangesRequested;

        // Rehacer la versión es trabajo de Diseño.
        if (!CommercialHasPendingWork) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
        ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "ClientChangesRequested", string.Join(" · ", cleanItems));
    }

    /// <summary>Diseño palomea (o despalomea) un cambio que pidió el cliente.</summary>
    public void SetClientChangeItemDone(Actor actor, IClock clock, Guid itemId, bool done, string? comment = null)
    {
        EnsureNotTerminal();
        var request = PendingClientChangeRequest
            ?? throw new InvalidNpiTransitionException("no-pending-client-changes", "No hay cambios del cliente pendientes.");
        if (!request.SetChecklistItemDone(itemId, done, actor, clock.UtcNow, comment))
            throw new NpiValidationException("client-change-item-not-found", "Ese cambio no pertenece a la solicitud pendiente del cliente.");
    }

    /// <summary>Acción 16 — Rechazo definitivo del cliente (terminal, decisión 7). Siempre lo ejecuta Comercial (decisión 8).</summary>
    public void RejectByClient(Actor actor, IClock clock, IBusinessCalendar calendar, string comment, Guid? evidenceAttachmentId) =>
        Close(Domain.TerminalReason.ClientRejection, actor, clock, calendar, comment, evidenceAttachmentId);

    /// <summary>Acción 17 — PrintCard cargado en Sistema. NO es terminal; sólo cierra DesignTimer (decisión 3).</summary>
    public void LoadPrintCardIntoSystem(Actor actor, IClock clock, IBusinessCalendar calendar)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Diseno);
        if (PrintCardLoadedAt is not null)
            throw new InvalidNpiTransitionException("printcard-already-loaded", "El PrintCard ya fue cargado en sistema.");
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard vigente.");
        if (!version.HasApprovalFrom(ApproverRole.Engineering))
            throw new InvalidNpiTransitionException("printcard-engineering-approval-missing", "El PrintCard requiere la aprobación de Ingeniería antes de cargarse en sistema.");
        if (!version.HasApprovalFrom(ApproverRole.Client))
            throw new InvalidNpiTransitionException("client-approval-missing", "El PrintCard requiere la aprobación del cliente antes de cargarse en sistema.");
        if (!version.IsFullyApproved)
            throw new InvalidNpiTransitionException("printcard-not-fully-approved", "La versión vigente no está totalmente aprobada.");

        var now = clock.UtcNow;
        PrintCardLoadedAt = now;
        PrintCardLoadedByUserId = actor.UserId;
        PrintCardLoadedVersionId = version.Id;
        DesignAreaStatus = DesignAreaStatus.PrintCardLoaded;

        DesignTimer = DesignTimer.Close(clock, calendar);
        AppendTimerEvent(TimerStage.Design, TimerAction.Close, now, actor.UserId, DesignTimer.AccumulatedBusinessMs);
        ResumeTimer(TimerStage.Engineering, actor, clock, now); // le toca registrar el PT

        Audit(actor, now, "PrintCardLoadedInSystem", null);
    }

    /// <summary>Acción 18 — Registrar PT en SAP.</summary>
    public void RegisterSapPt(Actor actor, IClock clock, IBusinessCalendar calendar, string codigoPT, string? ptName, string? sapTable = null)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Ingenieria);
        if (string.IsNullOrWhiteSpace(codigoPT))
            throw new NpiValidationException("codigo-pt-required", "El código PT es obligatorio.");
        if (PrintCardLoadedAt is null)
            throw new InvalidNpiTransitionException("printcard-not-loaded", "Debe cargarse el PrintCard en sistema antes de registrar el PT en SAP.");
        if (PtRegisteredAt is not null)
            throw new InvalidNpiTransitionException("pt-already-registered", "El PT ya fue registrado en SAP.");
        if (PendingEngineeringChangeRequest is not null || PendingEngineeringReconfirmation is not null || PendingCommercialEdit is not null)
            throw new InvalidNpiTransitionException("engineering-changes-open", "Antes de cargar el PT, Comercial debe atender los cambios y tú reconfirmar la factibilidad.");
        var table = string.IsNullOrWhiteSpace(sapTable) ? null : sapTable.Trim('\r', '\n');
        if (table is { Length: > PtSapTableMaxLength })
            throw new NpiValidationException("pt-sap-table-too-long", "La tabla pegada es demasiado grande: pega sólo las filas del PT.");

        var now = clock.UtcNow;
        CodigoPT = codigoPT;
        PtName = ptName;
        PtSapTable = table;
        PtRegisteredAt = now;
        PtRegisteredByUserId = actor.UserId;
        EngineeringAreaStatus = EngineeringAreaStatus.PtRegistered;
        PauseTimer(TimerStage.Engineering, actor, clock, calendar, now); // espera el Pedido de Comercial
        CommercialTimer = CommercialTimer.StartOrResume(clock);              // le toca registrar el Pedido
        AppendTimerEvent(TimerStage.Commercial, TimerAction.Resume, now, actor.UserId);

        Audit(actor, now, "SapPtRegistered", codigoPT);
    }

    /// <summary>Acción 19 — Registrar Pedido en SAP.</summary>
    public void RegisterSapOrder(Actor actor, IClock clock, IBusinessCalendar calendar, string pedidoSapNumber)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Comercial);
        if (string.IsNullOrWhiteSpace(pedidoSapNumber))
            throw new NpiValidationException("pedido-sap-required", "El número de Pedido en SAP es obligatorio.");
        if (CodigoPT is null)
            throw new InvalidNpiTransitionException("pt-not-registered", "Debe registrarse el PT en SAP antes del Pedido.");
        if (PedidoSapRegisteredAt is not null)
            throw new InvalidNpiTransitionException("pedido-sap-already-registered", "El Pedido en SAP ya fue registrado.");

        var now = clock.UtcNow;
        PedidoSapNumber = pedidoSapNumber;
        PedidoSapRegisteredAt = now;
        PedidoSapRegisteredByUserId = actor.UserId;
        PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
        ResumeTimer(TimerStage.Engineering, actor, clock, now); // le toca la ET

        Audit(actor, now, "SapOrderRegistered", pedidoSapNumber);
    }

    /// <summary>Acción 20 — Registrar ET Creada (hito separado de la carga en SisPro, decisión 9).</summary>
    public void RegisterEt(Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Ingenieria);
        if (CodigoPT is null || PedidoSapNumber is null)
            throw new InvalidNpiTransitionException("et-preconditions-not-met", "La ET requiere PT y Pedido en SAP registrados.");
        if (EtCreatedAt is not null)
            throw new InvalidNpiTransitionException("et-already-created", "La ET ya fue creada.");

        var now = clock.UtcNow;
        EtCreatedAt = now;
        EtCreatedByUserId = actor.UserId;
        EngineeringAreaStatus = EngineeringAreaStatus.EtCreated;

        Audit(actor, now, "EtCreated", null);
    }

    /// <summary>Acción 21 — Registrar ET cargada en SisPro. Evalúa y, si corresponde, dispara el cierre exitoso del ciclo (decisión 4).</summary>
    public void LoadEtIntoSispro(Actor actor, IClock clock, IBusinessCalendar calendar)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Ingenieria);
        if (EtCreatedAt is null)
            throw new InvalidNpiTransitionException("et-not-created", "La ET debe crearse antes de cargarse en SisPro.");
        if (EtLoadedInSisproAt is not null)
            throw new InvalidNpiTransitionException("et-already-loaded", "La ET ya fue cargada en SisPro.");

        var now = clock.UtcNow;
        EtLoadedInSisproAt = now;
        EtLoadedInSisproByUserId = actor.UserId;
        EngineeringAreaStatus = EngineeringAreaStatus.EtLoadedInSispro;

        EngineeringTimer = EngineeringTimer.Close(clock, calendar);
        AppendTimerEvent(TimerStage.Engineering, TimerAction.Close, now, actor.UserId, EngineeringTimer.AccumulatedBusinessMs);

        Audit(actor, now, "EtLoadedInSispro", null);

        if (IsEligibleForCompletion())
        {
            CommercialTimer = CommercialTimer.Close(clock, calendar);
            GlobalTimer = GlobalTimer.Close(clock, calendar);
            AppendTimerEvent(TimerStage.Commercial, TimerAction.Close, now, actor.UserId, CommercialTimer.AccumulatedBusinessMs);
            AppendTimerEvent(TimerStage.Global, TimerAction.Close, now, actor.UserId, GlobalTimer.AccumulatedBusinessMs);

            LifecycleStatus = Domain.LifecycleStatus.Completed;
            Audit(actor, now, "NpiCycleCompleted", null);
            _domainEvents.Add(new NpiCycleCompletedEvent(Id, now));
        }
    }

    /// <summary>Condición de éxito del ciclo (decisión 4, invariante §4.2 de domain-model.md).</summary>
    private bool IsEligibleForCompletion() =>
        PrintCardLoadedAt is not null &&
        PtRegisteredAt is not null &&
        PedidoSapRegisteredAt is not null &&
        EtLoadedInSisproAt is not null;

    /// <summary>Acción 22 — Rechazo temporal por falta de insumos (Ingeniería, no terminal).</summary>
    public void RejectSuppliesTemporary(Actor actor, IClock clock, IBusinessCalendar calendar, string? note)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Ingenieria);
        if (SuppliesTemporaryRejection is not null)
            throw new InvalidNpiTransitionException("supplies-rejection-already-open", "Ya existe un rechazo temporal por insumos abierto.");
        if (EngineeringFeasibility is not null || CodigoPT is not null || EtLoadedInSisproAt is not null)
            throw new InvalidNpiTransitionException("supplies-rejection-not-eligible", "El rechazo temporal por insumos sólo aplica antes de que Ingeniería declare factibilidad y antes de registrar PT/ET.");

        var now = clock.UtcNow;
        SuppliesTemporaryRejection = new SuppliesTemporaryRejectionInfo(now, actor.UserId, note);
        EngineeringAreaStatus = EngineeringAreaStatus.SuppliesTemporaryRejection;

        EngineeringTimer = EngineeringTimer.Pause(clock, calendar);
        AppendTimerEvent(TimerStage.Engineering, TimerAction.Pause, now, actor.UserId, note: "Rechazo temporal por falta de insumos");
        if (DesignTimer.IsRunning)
        {
            DesignTimer = DesignTimer.Pause(clock, calendar);
            AppendTimerEvent(TimerStage.Design, TimerAction.Pause, now, actor.UserId, note: "Rechazo temporal por falta de insumos");
        }
        // Resolverlo (acuerdo o rechazo total) le toca a Comercial: su reloj corre hasta entonces.
        CommercialTimer = CommercialTimer.StartOrResume(clock);
        AppendTimerEvent(TimerStage.Commercial, TimerAction.Resume, now, actor.UserId, note: "Rechazo temporal por falta de insumos");

        Audit(actor, now, "SuppliesTemporaryRejection", note);
    }

    /// <summary>Acción 23 — Acuerdo para solventar insumos (Comercial). Reactiva Ingeniería.</summary>
    public void ResolveSuppliesAgreement(Actor actor, IClock clock, IBusinessCalendar calendar, string text, Guid evidenceAttachmentId)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Comercial);
        if (SuppliesTemporaryRejection is null)
            throw new InvalidNpiTransitionException("no-open-supplies-rejection", "No hay un rechazo temporal por insumos abierto.");
        if (string.IsNullOrWhiteSpace(text))
            throw new NpiValidationException("supplies-agreement-text-required", "El acuerdo de insumos requiere texto.");

        var now = clock.UtcNow;
        SuppliesAgreement = new SuppliesAgreementInfo(text, now, actor.UserId, evidenceAttachmentId);
        SuppliesTemporaryRejection = null;
        EngineeringAreaStatus = EngineeringAreaStatus.Assigned;

        CommercialTimer = CommercialTimer.Pause(clock, calendar);
        EngineeringTimer = EngineeringTimer.StartOrResume(clock);
        AppendTimerEvent(TimerStage.Commercial, TimerAction.Pause, now, actor.UserId);
        AppendTimerEvent(TimerStage.Engineering, TimerAction.Resume, now, actor.UserId, note: "Acuerdo para solventar insumos");

        Audit(actor, now, "SuppliesAgreementRegistered", text);
    }

    /// <summary>Acción 24 — Rechazo total por falta de insumos (terminal).</summary>
    public void RejectSuppliesTotal(Actor actor, IClock clock, IBusinessCalendar calendar, string? note = null) =>
        Close(Domain.TerminalReason.SuppliesRejectionTotal, actor, clock, calendar, note, null);

    /// <summary>Acción 25 — Solicitar información (Diseño → Cliente, → Comercial o → Ingeniería).</summary>
    public void RequestInformation(Actor actor, IClock clock, IBusinessCalendar calendar, InformationRequestType type, string text, IReadOnlyList<Guid>? imageAttachmentIds = null)
    {
        EnsureNotTerminal();
        EnsureNoClosureUnderReview(AreaType.Diseno);
        if (string.IsNullOrWhiteSpace(text))
            throw new NpiValidationException("information-request-text-required", "La solicitud de información requiere texto.");
        if (_informationRequests.Any(r => r.Type == type && r.Status == InformationRequestStatus.Pending))
            throw new InvalidNpiTransitionException("information-request-already-pending", $"Ya existe una solicitud de información pendiente de tipo {type}.");
        var images = ValidRequestImages(imageAttachmentIds);

        var now = clock.UtcNow;
        var informationRequest = new InformationRequest(Guid.NewGuid(), type, text, actor.UserId, actor.Name, now);
        informationRequest.SetImages(images);
        _informationRequests.Add(informationRequest);
        DesignAreaStatus = DesignAreaStatus.AwaitingInformation;

        PauseTimer(TimerStage.Design, actor, clock, calendar, now);

        if (type == InformationRequestType.Commercial)
        {
            CommercialTimer = CommercialTimer.StartOrResume(clock);
            AppendTimerEvent(TimerStage.Commercial, TimerAction.Resume, now, actor.UserId);
        }
        // La duda le toca a Ingeniería: su reloj corre mientras contesta (sólo si ya arrancó; nunca reabre uno cerrado).
        if (type == InformationRequestType.Engineering) ResumeTimer(TimerStage.Engineering, actor, clock, now);

        Audit(actor, now, "InformationRequested", type.ToString());
    }

    /// <summary>Acción 26 — Resolver solicitud de información del cliente (registrado por Diseño).</summary>
    public void ResolveClientInformation(Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        var pending = _informationRequests.FirstOrDefault(r => r.Type == InformationRequestType.Client && r.Status == InformationRequestStatus.Pending)
            ?? throw new InvalidNpiTransitionException("no-pending-client-information-request", "No hay una solicitud de información pendiente de tipo Cliente.");

        var now = clock.UtcNow;
        pending.Resolve(now, actor.UserId);
        DesignAreaStatus = CurrentDesignAreaStatus();

        if (!HasPendingInformationRequest) ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "ClientInformationReceived", null);
    }

    /// <summary>Acción 27 — Responder solicitud de información a Diseño (Comercial).</summary>
    public void RespondCommercialInformation(Actor actor, IClock clock, IBusinessCalendar calendar, string responseText)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(responseText))
            throw new NpiValidationException("information-response-required", "La respuesta a Diseño es obligatoria.");
        var pending = _informationRequests.FirstOrDefault(r => r.Type == InformationRequestType.Commercial && r.Status == InformationRequestStatus.Pending)
            ?? throw new InvalidNpiTransitionException("no-pending-commercial-information-request", "No hay una solicitud de información pendiente de tipo Comercial.");

        var now = clock.UtcNow;
        pending.Resolve(now, actor.UserId, responseText);
        DesignAreaStatus = CurrentDesignAreaStatus();

        if (!CommercialHasPendingWork) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
        if (!HasPendingInformationRequest) ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "CommercialInformationSent", responseText);
    }

    /// <summary>
    /// Ingeniería contesta las dudas que le hizo Diseño (2026-11-04). Diseño retoma su reloj; el de
    /// Ingeniería se pausa si no le queda otra cosa pendiente.
    /// </summary>
    public void RespondEngineeringInformation(Actor actor, IClock clock, IBusinessCalendar calendar, string responseText)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(responseText))
            throw new NpiValidationException("information-response-required", "La respuesta a Diseño es obligatoria.");
        var pending = _informationRequests.FirstOrDefault(r => r.Type == InformationRequestType.Engineering && r.Status == InformationRequestStatus.Pending)
            ?? throw new InvalidNpiTransitionException("no-pending-engineering-information-request", "No hay dudas de Diseño pendientes para Ingeniería.");

        var now = clock.UtcNow;
        pending.Resolve(now, actor.UserId, responseText);
        DesignAreaStatus = CurrentDesignAreaStatus();

        if (!EngineeringHasPendingWork) PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);
        if (!HasPendingInformationRequest) ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "EngineeringInformationSent", responseText);
    }

    private bool HasPendingInformationRequest => _informationRequests.Any(r => r.Status == InformationRequestStatus.Pending);

    // ===== Editar o retirar una solicitud ya enviada (2026-11-04) =====
    // Antes, si a Diseño o Ingeniería se le olvidaba algo, se atoraba: sólo podía haber una solicitud
    // pendiente y no se podía tocar. Ahora quien la mandó la corrige (o le agrega fotos) mientras
    // nadie la ha contestado, o la retira si ya no hace falta.

    private InformationRequest PendingInformationRequest(Guid requestId) =>
        _informationRequests.FirstOrDefault(r => r.Id == requestId && r.Status == InformationRequestStatus.Pending)
            ?? throw new InvalidNpiTransitionException("information-request-not-pending", "Esa solicitud ya se contestó o se retiró: ya no se puede cambiar.");

    /// <summary>Diseño corrige el texto de una solicitud de información pendiente y/o le agrega imágenes.</summary>
    public void EditInformationRequest(Actor actor, IClock clock, Guid requestId, string text, IReadOnlyList<Guid>? addImageAttachmentIds = null)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(text))
            throw new NpiValidationException("information-request-text-required", "La solicitud de información requiere texto.");
        var request = PendingInformationRequest(requestId);
        var images = ValidRequestImages([.. request.ImageAttachmentIds, .. addImageAttachmentIds ?? []]);

        request.Edit(text.Trim());
        request.SetImages(images);
        Audit(actor, clock.UtcNow, "InformationRequestEdited", request.Type.ToString());
    }

    /// <summary>
    /// Diseño retira una solicitud de información pendiente (ya no la necesita). Su reloj regresa si
    /// no espera otra cosa; el de quien la iba a contestar se pausa si no le queda nada más.
    /// </summary>
    public void WithdrawInformationRequest(Actor actor, IClock clock, IBusinessCalendar calendar, Guid requestId)
    {
        EnsureNotTerminal();
        var request = PendingInformationRequest(requestId);

        var now = clock.UtcNow;
        request.Withdraw(now, actor.UserId);
        DesignAreaStatus = CurrentDesignAreaStatus();

        if (request.Type == InformationRequestType.Commercial && !CommercialHasPendingWork) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
        if (request.Type == InformationRequestType.Engineering && !EngineeringHasPendingWork) PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);
        if (!HasPendingInformationRequest) ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "InformationRequestWithdrawn", request.Type.ToString());
    }

    /// <summary>Solicitud de cambios a Comercial (de Ingeniería o de Diseño) que aún no se atiende.</summary>
    private ChangeRequest PendingCaptureChangeRequest(Guid requestId) =>
        _changeRequests.FirstOrDefault(c => c.Id == requestId && !c.IsResolved && c.ToArea == AreaType.Comercial
            && c.Origin == ChangeRequestOrigin.Internal && c.FromArea is AreaType.Ingenieria or AreaType.Diseno)
            ?? throw new InvalidNpiTransitionException("change-request-not-pending", "Esa solicitud de cambios ya se atendió o se retiró: ya no se puede cambiar.");

    /// <summary>
    /// Ingeniería o Diseño corrige su solicitud de cambios antes de que Comercial la atienda: marca o
    /// quita campos, cambia los comentarios o el motivo, y agrega imágenes. La foto de la captura que
    /// se tomó al pedirla no cambia (sirve para ver qué corrigió Comercial).
    /// </summary>
    public ChangeRequest EditChangeRequest(Actor actor, IClock clock, Guid requestId, string description, IReadOnlyList<string> fieldKeys, IReadOnlyDictionary<string, string>? fieldNotes = null, IReadOnlyList<Guid>? addImageAttachmentIds = null)
    {
        EnsureNotTerminal();
        var request = PendingCaptureChangeRequest(requestId);
        if (request.FromArea == AreaType.Diseno) RequireRejectionNote(description);
        else if (string.IsNullOrWhiteSpace(description) && fieldKeys.Count == 0)
            throw new NpiValidationException("change-request-empty", "Debe incluir una descripción o al menos un campo a modificar.");
        var images = ValidRequestImages([.. request.ImageAttachmentIds, .. addImageAttachmentIds ?? []]);

        request.Edit((description ?? string.Empty).Trim(), fieldKeys.Distinct().ToList());
        request.SetFieldNotes(CleanFieldNotes(fieldKeys, fieldNotes));
        request.SetImages(images);
        Audit(actor, clock.UtcNow, "ChangeRequestEdited", request.FromArea.ToString());
        return request;
    }

    /// <summary>
    /// Ingeniería o Diseño retira su solicitud de cambios antes de que Comercial la atienda. Queda en
    /// el historial como retirada; si al pedirla se retiró el PrintCard, regresa como estaba; los
    /// estatus y relojes vuelven a quien le toca.
    /// </summary>
    public ChangeRequest WithdrawChangeRequest(Actor actor, IClock clock, IBusinessCalendar calendar, Guid requestId)
    {
        EnsureNotTerminal();
        var request = PendingCaptureChangeRequest(requestId);

        var now = clock.UtcNow;
        request.Withdraw(actor, now);
        if (request.FromArea == AreaType.Ingenieria) RestorePrintCardRetiredByEngineering(request, clock, calendar);
        else if (CurrentPrintCardVersion is { IsRejected: true } retired && retired.RejectionReason == PrintCardRetiredByDesignReason
                 && retired.RejectedAt == request.RequestedAt)
            retired.ClearRejection();

        EngineeringAreaStatus = CurrentEngineeringAreaStatus();
        DesignAreaStatus = CurrentDesignAreaStatus();
        SyncAreaTimers(actor, clock, calendar, now);
        Audit(actor, now, "ChangeRequestWithdrawn", request.FromArea.ToString());
        return request;
    }

    /// <summary>
    /// Quita un adjunto subido por error (2026-11-09). Sólo quien lo subió o AdminTotal, con el NPI
    /// abierto, y sólo si no es evidencia de algo (PrintCard, aprobaciones, solicitudes, acuerdos,
    /// cierres): ésos son parte del historial. El archivo NO se borra del servidor (decisión del dueño):
    /// sólo sale de la lista, y la bitácora guarda su clave en disco por si hay que recuperarlo.
    /// </summary>
    public Attachment RemoveAttachment(Actor actor, IClock clock, Guid attachmentId)
    {
        EnsureNotTerminal();
        var attachment = _attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new NpiValidationException("attachment-not-found", "El archivo no existe en este NPI.");
        if (attachment.UploadedByUserId != actor.UserId && actor.Role != ApplicationRoles.AdminTotal)
            throw new InvalidNpiTransitionException("attachment-not-yours", "Sólo quien subió el archivo (o el administrador) puede eliminarlo.");
        if (AttachmentUse(attachmentId) is { } use)
            throw new InvalidNpiTransitionException("attachment-in-use", $"No se puede eliminar: es {use} y forma parte del historial del NPI.");

        _attachments.Remove(attachment);
        Audit(actor, clock.UtcNow, "AttachmentDeleted", attachment.IsLink ? $"{attachment.FileName} ({attachment.Url})" : $"{attachment.FileName} (archivo en el servidor: {attachment.StorageKey})");
        return attachment;
    }

    /// <summary>Para qué se usa un adjunto como evidencia; null si es un archivo suelto.</summary>
    private string? AttachmentUse(Guid id)
    {
        if (_printCardVersions.Any(v => v.AttachmentId == id)) return "el archivo de un PrintCard";
        if (_printCardVersions.Any(v => v.RejectionEvidenceAttachmentId == id || v.Approvals.Any(a => a.EvidenceAttachmentId == id)))
            return "la evidencia de una aprobación o un rechazo del PrintCard";
        if (_changeRequests.Any(c => c.EvidenceAttachmentId == id || c.ImageAttachmentIds.Contains(id))) return "parte de una solicitud de cambios";
        if (_informationRequests.Any(r => r.ImageAttachmentIds.Contains(id))) return "parte de una solicitud de información";
        if (SuppliesAgreement?.EvidenceAttachmentId == id || ClientRejection?.EvidenceAttachmentId == id) return "la evidencia de un acuerdo de insumos o de un rechazo del cliente";
        if (_sisproQuotes.Any(q => q.EvidenceAttachmentId == id)) return "la evidencia de la respuesta a una cotización";
        if (_closureProposals.Any(p => p.EvidenceAttachmentId == id)) return "la evidencia de una propuesta de cierre";
        return null;
    }

    /// <summary>Registro de metadatos de un archivo ya almacenado por IFileStorage (no valida contenido — eso es de Infrastructure).</summary>
    public Attachment RegisterAttachment(
        Actor actor,
        IClock clock,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        string? sha256Hash,
        AttachmentContext context)
    {
        var now = clock.UtcNow;
        var version = _attachments.Count(a => a.Context == context) + 1;
        var attachment = new Attachment(Guid.NewGuid(), fileName, contentType, sizeBytes, storageKey, sha256Hash, actor.UserId, now, context, version);
        _attachments.Add(attachment);
        Audit(actor, now, "AttachmentUploaded", $"context={context};version={version}");
        return attachment;
    }

    /// <summary>
    /// Registra un link externo como adjunto (misma lista, contexto y versión que un archivo, para
    /// que pueda servir también como evidencia). La URL ya viene validada por Application.
    /// </summary>
    public Attachment RegisterLink(Actor actor, IClock clock, string title, string url, AttachmentContext context)
    {
        var now = clock.UtcNow;
        var version = _attachments.Count(a => a.Context == context) + 1;
        var attachment = new Attachment(Guid.NewGuid(), title, Attachment.LinkContentType, 0, $"link:{Guid.NewGuid():N}", null, actor.UserId, now, context, version, url);
        _attachments.Add(attachment);
        Audit(actor, now, "LinkAdded", $"context={context};version={version}");
        return attachment;
    }

    /// <summary>Acción 29 — Cancelar (administrativo, ADR-024). Cierra los timers abiertos por higiene de datos.</summary>
    public void Cancel(Actor actor, IClock clock, IBusinessCalendar calendar, string reason)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(reason))
            throw new NpiValidationException("cancel-reason-required", "Cancelar un NPI requiere una razón.");

        var now = clock.UtcNow;
        CloseAllTimers(clock, calendar, now, actor.UserId);
        LifecycleStatus = Domain.LifecycleStatus.Cancelled;
        PendingClosureProposalIgnoringLifecycle?.Decide(ClosureProposalStatus.Superseded, actor, now, "El NPI se canceló.");

        Audit(actor, now, "NpiCancelled", reason);
    }

    // ===== Cierres terminales y su visto bueno (propuestas de cierre) =====
    // Los 5 cierres que puede hacer un área (no factibilidad de Ingeniería o de Diseño, rechazo del
    // cliente, rechazo total por insumos y rechazo por precio) se validan y se aplican aquí, en un solo
    // lugar: así el cierre directo de un admin y el que confirma sobre la propuesta de un usuario
    // normal exigen y hacen exactamente lo mismo. Quién puede cerrar directo lo decide Application.

    /// <summary>La propuesta de cierre que espera el visto bueno del admin (null si no hay o el NPI ya terminó).</summary>
    public ClosureProposal? PendingClosureProposal =>
        IsTerminal ? null : _closureProposals.FirstOrDefault(p => p.Status == ClosureProposalStatus.Pending);

    private bool IsTerminal => LifecycleStatus is Domain.LifecycleStatus.Completed or Domain.LifecycleStatus.Rejected or Domain.LifecycleStatus.Cancelled;

    private void Close(TerminalReason reason, Actor actor, IClock clock, IBusinessCalendar calendar, string? note, Guid? evidenceAttachmentId)
    {
        EnsureCanClose(reason, note, evidenceAttachmentId);
        ApplyClosure(reason, actor, clock, calendar, note, evidenceAttachmentId);
    }

    /// <summary>Precondiciones de cada cierre (las mismas para el cierre directo, la propuesta y su confirmación).</summary>
    private void EnsureCanClose(TerminalReason reason, string? note, Guid? evidenceAttachmentId)
    {
        switch (reason)
        {
            case Domain.TerminalReason.NoFeasibility:
                EnsureNotTerminal();
                break;

            case Domain.TerminalReason.DesignNoFeasibility:
                EnsureActive();
                if (AssignedDesignUserId is null)
                    throw new InvalidNpiTransitionException("design-not-assigned", "El NPI no tiene un diseñador asignado.");
                if (DesignFeasibility is not null)
                    throw new InvalidNpiTransitionException("design-feasibility-already-approved", "La factibilidad de Diseño ya fue aprobada.");
                RequireRejectionNote(note);
                break;

            case Domain.TerminalReason.PriceRejection:
                EnsureActive();
                var quote = CurrentSisproQuote
                    ?? throw new InvalidNpiTransitionException("no-sispro-quote", "Primero liga la cotización de SISPRO: el cliente no puede rechazar un precio que no ha visto. Para detener el NPI por otro motivo usa Cancelar.");
                if (quote.Status is not (SisproQuoteStatus.AwaitingClient or SisproQuoteStatus.AdjustmentRequested))
                    throw new InvalidNpiTransitionException("sispro-quote-not-awaiting-client", "La cotización vigente no está esperando la respuesta del cliente.");
                if (string.IsNullOrWhiteSpace(note))
                    throw new NpiValidationException("price-rejection-comment-required", "El rechazo por precio exige un comentario.");
                if (note.Trim().Length > 2000)
                    throw new NpiValidationException("sispro-quote-note-too-long", "El comentario no puede exceder 2000 caracteres.");
                break;

            case Domain.TerminalReason.ClientRejection:
                EnsureNotTerminal();
                if (string.IsNullOrWhiteSpace(note))
                    throw new NpiValidationException("client-rejection-comment-required", "El rechazo definitivo del cliente exige un comentario.");
                if (PrintCardSentToClientAt is null)
                    throw new InvalidNpiTransitionException("printcard-not-sent-to-client", "Primero registra «Enviar a Cliente»: el cliente no puede rechazar algo que no ha visto. Para detener el NPI por otro motivo usa Cancelar.");
                break;

            case Domain.TerminalReason.SuppliesRejectionTotal:
                EnsureNotTerminal();
                if (SuppliesTemporaryRejection is null)
                    throw new InvalidNpiTransitionException("no-open-supplies-rejection", "No hay un rechazo temporal por insumos abierto.");
                break;
        }

        if (evidenceAttachmentId is { } evidenceId && _attachments.All(a => a.Id != evidenceId))
            throw new NpiValidationException("evidence-not-found", "La evidencia indicada no pertenece a este NPI.");
    }

    private void ApplyClosure(TerminalReason reason, Actor actor, IClock clock, IBusinessCalendar calendar, string? note, Guid? evidenceAttachmentId)
    {
        var now = clock.UtcNow;
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        switch (reason)
        {
            case Domain.TerminalReason.NoFeasibility:
                EngineeringAreaStatus = EngineeringAreaStatus.NoFeasibility;
                break;
            case Domain.TerminalReason.DesignNoFeasibility:
                DesignAreaStatus = DesignAreaStatus.NoFeasibility;
                break;
            case Domain.TerminalReason.PriceRejection:
                CurrentSisproQuote!.Respond(SisproQuoteStatus.Rejected, actor, now, trimmed, evidenceAttachmentId);
                break;
            case Domain.TerminalReason.ClientRejection:
                ClientRejection = new ClientRejectionInfo(now, actor.UserId, trimmed!, evidenceAttachmentId);
                break;
        }

        CloseAllTimers(clock, calendar, now, actor.UserId);
        LifecycleStatus = Domain.LifecycleStatus.Rejected;
        TerminalReason = reason;

        // Si alguien más ya había propuesto este mismo cierre, queda confirmado por quien cerró; otro
        // cierre deja la propuesta sin efecto.
        if (PendingClosureProposalIgnoringLifecycle is { } pending)
            pending.Decide(pending.Reason == reason ? ClosureProposalStatus.Confirmed : ClosureProposalStatus.Superseded, actor, now, null);

        Audit(actor, now, reason switch
        {
            Domain.TerminalReason.NoFeasibility => "NpiRejectedNoFeasibility",
            Domain.TerminalReason.DesignNoFeasibility => "NpiRejectedDesignNoFeasibility",
            Domain.TerminalReason.PriceRejection => "NpiRejectedByPrice",
            Domain.TerminalReason.ClientRejection => "NpiRejectedByClient",
            _ => "NpiRejectedSuppliesTotal",
        }, trimmed);
        _domainEvents.Add(new NpiRejectedEvent(Id, reason, now));
    }

    private static readonly HashSet<string> ClosingActions =
    [
        "NpiRejectedNoFeasibility", "NpiRejectedDesignNoFeasibility", "NpiRejectedByPrice",
        "NpiRejectedByClient", "NpiRejectedSuppliesTotal", "NpiCancelled",
    ];

    /// <summary>
    /// El evento con que se cerró el NPI (rechazo o cancelación), con el motivo escrito en Details y
    /// quién y cuándo. null si no está cerrado. Antes el motivo de la no factibilidad sólo quedaba en
    /// la bitácora y Comercial veía "No factible" sin saber por qué (bug NPI-0034/0035, 2026-11-09).
    /// </summary>
    public AuditEvent? ClosingEvent =>
        LifecycleStatus is Domain.LifecycleStatus.Rejected or Domain.LifecycleStatus.Cancelled
            ? _auditEvents.Where(e => ClosingActions.Contains(e.Action)).OrderBy(e => e.OccurredAt).LastOrDefault()
            : null;

    private ClosureProposal? PendingClosureProposalIgnoringLifecycle =>
        _closureProposals.FirstOrDefault(p => p.Status == ClosureProposalStatus.Pending);

    /// <summary>
    /// Un usuario normal propone cerrar el NPI: el NPI no se cierra, queda esperando el visto bueno
    /// del admin del área. Se validan las mismas condiciones que el cierre directo, y el motivo es
    /// obligatorio siempre (es lo que lee el admin para decidir).
    /// </summary>
    public ClosureProposal ProposeClosure(TerminalReason reason, Actor actor, IClock clock, string? note, Guid? evidenceAttachmentId)
    {
        EnsureCanClose(reason, note, evidenceAttachmentId);
        if (string.IsNullOrWhiteSpace(note))
            throw new NpiValidationException("closure-proposal-note-required", "Explica el motivo: es lo que verá el administrador para darte el visto bueno.");
        if (note.Trim().Length > 2000)
            throw new NpiValidationException("closure-proposal-note-too-long", "El motivo no puede exceder 2000 caracteres.");
        if (PendingClosureProposal is not null)
            throw new InvalidNpiTransitionException("closure-proposal-pending", "Ya hay una propuesta de cierre de este NPI esperando el visto bueno del administrador.");

        var now = clock.UtcNow;
        var proposal = new ClosureProposal(Guid.NewGuid(), reason, ClosureProposal.AreaOf(reason), actor.UserId, actor.Name, now, note.Trim(), evidenceAttachmentId);
        _closureProposals.Add(proposal);
        Audit(actor, now, "ClosureProposed", $"{reason}: {note.Trim()}");
        return proposal;
    }

    /// <summary>El admin da luz verde: el NPI se cierra con el motivo y la nota de quien lo propuso.</summary>
    public ClosureProposal ConfirmClosureProposal(Actor admin, IClock clock, IBusinessCalendar calendar, string? comment)
    {
        var proposal = PendingClosureProposal
            ?? throw new InvalidNpiTransitionException("no-pending-closure-proposal", "No hay una propuesta de cierre esperando visto bueno.");
        // El NPI pudo avanzar mientras esperaba (p. ej. el cliente aceptó el precio): se revalida.
        EnsureCanClose(proposal.Reason, proposal.Note, proposal.EvidenceAttachmentId);

        var now = clock.UtcNow;
        proposal.Decide(ClosureProposalStatus.Confirmed, admin, now, comment);
        Audit(admin, now, "ClosureProposalConfirmed", $"Propuesto por {proposal.ProposedByName}{(proposal.DecisionNote is null ? "" : $": {proposal.DecisionNote}")}");
        ApplyClosure(proposal.Reason, admin, clock, calendar, proposal.Note, proposal.EvidenceAttachmentId);
        return proposal;
    }

    /// <summary>El admin no autoriza el cierre: el NPI sigue su curso. El comentario es obligatorio (lo lee quien lo propuso).</summary>
    public ClosureProposal DeclineClosureProposal(Actor admin, IClock clock, string comment)
    {
        var proposal = PendingClosureProposal
            ?? throw new InvalidNpiTransitionException("no-pending-closure-proposal", "No hay una propuesta de cierre esperando visto bueno.");
        if (string.IsNullOrWhiteSpace(comment))
            throw new NpiValidationException("closure-decline-comment-required", "Explica por qué no autorizas el cierre: es lo que leerá quien lo propuso.");
        if (comment.Trim().Length > 2000)
            throw new NpiValidationException("closure-decline-comment-too-long", "El comentario no puede exceder 2000 caracteres.");

        var now = clock.UtcNow;
        proposal.Decide(ClosureProposalStatus.Declined, admin, now, comment);
        Audit(admin, now, "ClosureProposalDeclined", $"Propuesto por {proposal.ProposedByName}: {comment.Trim()}");
        return proposal;
    }

    /// <summary>Quien propuso el cierre lo retira antes de que el admin decida (Application verifica que sea él).</summary>
    public ClosureProposal WithdrawClosureProposal(Actor actor, IClock clock)
    {
        var proposal = PendingClosureProposal
            ?? throw new InvalidNpiTransitionException("no-pending-closure-proposal", "No hay una propuesta de cierre esperando visto bueno.");

        var now = clock.UtcNow;
        proposal.Decide(ClosureProposalStatus.Withdrawn, actor, now, null);
        Audit(actor, now, "ClosureProposalWithdrawn", proposal.Reason.ToString());
        return proposal;
    }

    /// <summary>
    /// Mientras un cierre del área espera el visto bueno, esa área no avanza el NPI (ni el usuario ni
    /// su admin): primero se decide si se cierra. Las otras áreas siguen en paralelo.
    /// </summary>
    private void EnsureNoClosureUnderReview(AreaType area)
    {
        if (PendingClosureProposal is { } pending && pending.Area == area)
            throw new InvalidNpiTransitionException("closure-proposal-pending",
                $"{pending.ProposedByName} propuso cerrar el NPI y está esperando el visto bueno del administrador. Mientras tanto no se puede avanzar en esta área.");
    }

    private void CloseAllTimers(IClock clock, IBusinessCalendar calendar, DateTimeOffset now, Guid actorUserId)
    {
        if (EngineeringTimer.HasStarted && !EngineeringTimer.IsClosed)
        {
            EngineeringTimer = EngineeringTimer.Close(clock, calendar);
            AppendTimerEvent(TimerStage.Engineering, TimerAction.Close, now, actorUserId, EngineeringTimer.AccumulatedBusinessMs);
        }
        if (DesignTimer.HasStarted && !DesignTimer.IsClosed)
        {
            DesignTimer = DesignTimer.Close(clock, calendar);
            AppendTimerEvent(TimerStage.Design, TimerAction.Close, now, actorUserId, DesignTimer.AccumulatedBusinessMs);
        }
        if (CommercialTimer.HasStarted && !CommercialTimer.IsClosed)
        {
            CommercialTimer = CommercialTimer.Close(clock, calendar);
            AppendTimerEvent(TimerStage.Commercial, TimerAction.Close, now, actorUserId, CommercialTimer.AccumulatedBusinessMs);
        }
        if (GlobalTimer.HasStarted && !GlobalTimer.IsClosed)
        {
            GlobalTimer = GlobalTimer.Close(clock, calendar);
            AppendTimerEvent(TimerStage.Global, TimerAction.Close, now, actorUserId, GlobalTimer.AccumulatedBusinessMs);
        }
    }

    /// <summary>Las acciones nuevas exigen un NPI enviado y abierto (no un borrador ni uno cerrado).</summary>
    private void EnsureActive()
    {
        EnsureNotTerminal();
        if (LifecycleStatus != Domain.LifecycleStatus.Active)
            throw new InvalidNpiTransitionException("npi-not-active", "El NPI todavía no se ha enviado.");
    }

    private void EnsureNotTerminal()
    {
        if (LifecycleStatus is Domain.LifecycleStatus.Completed or Domain.LifecycleStatus.Rejected or Domain.LifecycleStatus.Cancelled)
            throw new InvalidNpiTransitionException("npi-terminal", "El NPI ya se encuentra en un estado terminal; no admite más acciones de workflow.");
    }

    // ===== Deshacer y reabrir (2026-11-03) =====
    // Deshacer: quien aprobó o rechazó algo por error lo puede regresar mientras nadie más haya
    // actuado después (su acción es lo último que pasó en el NPI). Retirar una solicitud de cambios
    // es el mismo caso: mientras Comercial no la atienda. Reabrir: sólo AdminTotal, con motivo.

    /// <summary>Acciones que se pueden deshacer (nombre del evento de bitácora).</summary>
    private static readonly HashSet<string> UndoableActions =
    [
        "EngineeringFeasibilityApproved", "DesignFeasibilityApproved", "ArtworkValidated",
        "PrintCardApproved", "PrintCardRejectedByEngineering", "PrintCardSentToClient",
        "EngineeringRequestedChanges", "DesignRequestedChanges", "ArtworkRejected", "CommercialEditStarted",
    ];

    /// <summary>Eventos que no cuentan como "alguien actuó después" (adjuntos y lo que se registra solo).</summary>
    private static readonly HashSet<string> IgnoredForUndo = ["AttachmentUploaded", "LinkAdded", "AttachmentDeleted", "DesignInstructionsUpdated", "ChangeRequestEdited", "InformationRequestEdited", "PrintCardFullyApproved", "SisproQuoteWaitReleased", "PrintCardRetiredByDesign", "PrintCardRetiredByEngineering", "PrintCardUnloadedByEngineering", "PrintCardRetiredByCommercial"];

    /// <summary>Lo último que pasó en el NPI, sin contar los eventos de IgnoredForUndo.</summary>
    private AuditEvent? LastRelevantAuditEvent =>
        _auditEvents.Where(e => !IgnoredForUndo.Contains(e.Action)).OrderBy(e => e.OccurredAt).LastOrDefault();

    /// <summary>La acción que se puede deshacer ahora (y quién puede hacerlo); null si no hay ninguna.</summary>
    public AuditEvent? UndoableLastAction =>
        LifecycleStatus == Domain.LifecycleStatus.Active && LastRelevantAuditEvent is { } last && UndoableActions.Contains(last.Action) ? last : null;

    /// <summary>Deshace la última acción del NPI. Sólo quien la hizo, y sólo si nadie más actuó después.</summary>
    public void UndoLastAction(Actor actor, IClock clock, IBusinessCalendar calendar)
    {
        EnsureActive();
        var last = UndoableLastAction
            ?? throw new InvalidNpiTransitionException("nothing-to-undo", "No hay nada que deshacer: alguien ya actuó después o la última acción no se puede deshacer.");
        if (last.ActorUserId != actor.UserId)
            throw new InvalidNpiTransitionException("undo-not-yours", "Sólo quien hizo la acción puede deshacerla.");

        switch (last.Action)
        {
            case "EngineeringFeasibilityApproved":
                _feasibilityReviews.RemoveAll(f => f.Area == AreaType.Ingenieria);
                break;
            case "DesignFeasibilityApproved":
                _feasibilityReviews.RemoveAll(f => f.Area == AreaType.Diseno);
                break;
            case "ArtworkValidated":
                ArtesValidadasAt = null;
                ArtesValidadasByUserId = null;
                break;
            case "PrintCardApproved":
                CurrentPrintCardVersion!.RemoveApproval(last.Details == "role=Client" ? ApproverRole.Client : last.Details == "role=Design" ? ApproverRole.Design : ApproverRole.Engineering);
                break;
            case "PrintCardRejectedByEngineering":
                CurrentPrintCardVersion!.ClearRejection();
                break;
            case "PrintCardSentToClient":
                PrintCardSentToClientAt = null;
                PrintCardSentToClientByUserId = null;
                break;
            case "EngineeringRequestedChanges":
                var engineeringRequest = RemoveLatestRequest(c => c.FromArea == AreaType.Ingenieria && c.ToArea == AreaType.Comercial);
                RestorePrintCardRetiredByEngineering(engineeringRequest, clock, calendar);
                break;
            case "CommercialEditStarted":
                var commercialRequest = RemoveLatestRequest(c => c.Origin == ChangeRequestOrigin.CommercialEdit);
                RestorePrintCardRetiredByEngineering(commercialRequest, clock, calendar, PrintCardRetiredByCommercialReason);
                break;
            case "DesignRequestedChanges":
                var designRequest = RemoveLatestRequest(c => c.FromArea == AreaType.Diseno && c.Origin == ChangeRequestOrigin.Internal);
                // Si al pedirlos se retiró la versión del PrintCard en revisión, regresa a donde estaba.
                if (CurrentPrintCardVersion is { IsRejected: true } retired && retired.RejectionReason == PrintCardRetiredByDesignReason
                    && retired.RejectedAt == designRequest.RequestedAt)
                    retired.ClearRejection();
                break;
            case "ArtworkRejected":
                RemoveLatestRequest(c => c.Origin == ChangeRequestOrigin.ArtworkRejected);
                break;
        }

        var now = clock.UtcNow;
        EngineeringAreaStatus = CurrentEngineeringAreaStatus();
        DesignAreaStatus = CurrentDesignAreaStatus();
        SyncAreaTimers(actor, clock, calendar, now);
        Audit(actor, now, "ActionUndone", last.Action);
    }

    /// <summary>Quita la solicitud pendiente más reciente que cumpla la condición (aún no atendida).</summary>
    /// <summary>Deshacer una solicitud de Ingeniería que retiró el PrintCard: la versión (y su carga en sistema) regresan.</summary>
    private void RestorePrintCardRetiredByEngineering(ChangeRequest request, IClock clock, IBusinessCalendar calendar, string reason = PrintCardRetiredByEngineeringReason)
    {
        if (CurrentPrintCardVersion is not { IsRejected: true } retired || retired.RejectionReason != reason
            || retired.RejectedAt != request.RequestedAt)
            return;
        retired.ClearRejection();

        var unloaded = _auditEvents.FirstOrDefault(e => e.Action == "PrintCardUnloadedByEngineering" && e.OccurredAt == request.RequestedAt);
        if (unloaded?.Details is not { } details) return;
        var parts = details.Split(';').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
        PrintCardLoadedAt = DateTimeOffset.Parse(parts["loadedAt"], System.Globalization.CultureInfo.InvariantCulture);
        PrintCardLoadedByUserId = Guid.TryParse(parts.GetValueOrDefault("by"), out var by) ? by : null;
        PrintCardLoadedVersionId = Guid.TryParse(parts.GetValueOrDefault("version"), out var version) ? version : null;
        DesignTimer = DesignTimer.Close(clock, calendar);
    }

    private ChangeRequest RemoveLatestRequest(Func<ChangeRequest, bool> match)
    {
        var request = _changeRequests.Where(c => match(c) && !c.IsResolved).OrderByDescending(c => c.RequestedAt).FirstOrDefault()
            ?? throw new InvalidNpiTransitionException("nothing-to-undo", "La solicitud ya fue atendida: no se puede retirar.");
        _changeRequests.Remove(request);
        return request;
    }

    /// <summary>
    /// Reabre un NPI cerrado por error (no factibilidad de Ingeniería o de Diseño, o rechazo del
    /// cliente). Sólo AdminTotal (lo valida Application), con motivo. Los cronómetros se reabren con
    /// su acumulado; el tiempo que estuvo cerrado no se cuenta.
    /// </summary>
    public void Reopen(Actor actor, IClock clock, IBusinessCalendar calendar, string reason)
    {
        if (LifecycleStatus != Domain.LifecycleStatus.Rejected)
            throw new InvalidNpiTransitionException("npi-not-rejected", "Sólo se puede reabrir un NPI rechazado.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new NpiValidationException("reopen-reason-required", "Explica por qué se reabre: queda en el historial.");
        if (reason.Trim().Length > 2000)
            throw new NpiValidationException("reopen-reason-too-long", "El motivo no puede exceder 2000 caracteres.");
        switch (TerminalReason)
        {
            case Domain.TerminalReason.NoFeasibility:
            case Domain.TerminalReason.DesignNoFeasibility:
                break;
            case Domain.TerminalReason.ClientRejection:
                ClientRejection = null;
                break;
            default:
                throw new InvalidNpiTransitionException("reopen-not-supported", "Este cierre no se puede reabrir desde el sistema.");
        }

        var now = clock.UtcNow;
        var previousReason = TerminalReason;
        LifecycleStatus = Domain.LifecycleStatus.Active;
        TerminalReason = null;
        EngineeringAreaStatus = CurrentEngineeringAreaStatus();
        DesignAreaStatus = CurrentDesignAreaStatus();

        EngineeringTimer = EngineeringTimer.Reopen();
        DesignTimer = DesignTimer.Reopen();
        CommercialTimer = CommercialTimer.Reopen();
        GlobalTimer = GlobalTimer.Reopen();
        ResumeTimer(TimerStage.Global, actor, clock, now);
        SyncAreaTimers(actor, clock, calendar, now);

        Audit(actor, now, "NpiReopened", $"{previousReason}: {reason.Trim()}");
    }

    /// <summary>Estado de Ingeniería a partir de los hechos (para deshacer y reabrir).</summary>
    private EngineeringAreaStatus CurrentEngineeringAreaStatus()
    {
        if (EtLoadedInSisproAt is not null) return EngineeringAreaStatus.EtLoadedInSispro;
        if (EtCreatedAt is not null) return EngineeringAreaStatus.EtCreated;
        if (PtRegisteredAt is not null) return EngineeringAreaStatus.PtRegistered;
        if (EngineeringFeasibility is not null) return EngineeringAreaStatus.FeasibilityApproved;
        if (PendingEngineeringChangeRequest is not null) return EngineeringAreaStatus.ChangesRequested;
        if (SuppliesTemporaryRejection is not null) return EngineeringAreaStatus.SuppliesTemporaryRejection;
        return AssignedEngineeringUserId is not null ? EngineeringAreaStatus.Assigned : EngineeringAreaStatus.Requested;
    }

    /// <summary>A Diseño le toca trabajar: arrancó su parte y no espera a Ingeniería, al cliente ni a Comercial.</summary>
    private bool DesignHasPendingWork =>
        EngineeringFeasibility is not null && ArtesValidadasAt is not null && !AwaitingSisproQuote
        && PrintCardLoadedAt is null && !PrintCardAwaitingEngineering && !PrintCardAwaitingClient
        && PendingDesignChangeRequest is null && PendingEngineeringChangeRequest is null && PendingCommercialEdit is null && PendingArtworkRejection is null
        && !HasPendingInformationRequest;

    /// <summary>Corre o pausa el reloj de cada área según a quién le toca ahora (después de deshacer o reabrir).</summary>
    private void SyncAreaTimers(Actor actor, IClock clock, IBusinessCalendar calendar, DateTimeOffset now)
    {
        if (EngineeringHasPendingWork) ResumeTimer(TimerStage.Engineering, actor, clock, now);
        else PauseTimer(TimerStage.Engineering, actor, clock, calendar, now);

        if (CommercialHasPendingWork) RunCommercialTimer(actor, clock, now, "Se deshizo o reabrió un paso");
        else PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);

        if (DesignHasPendingWork) ResumeDesignTimer(actor, clock, now);
        else PauseTimer(TimerStage.Design, actor, clock, calendar, now);
    }

    private void Audit(Actor actor, DateTimeOffset at, string action, string? details)
        => _auditEvents.Add(new AuditEvent(Guid.NewGuid(), actor.UserId, actor.Role, at, action, details));

    private void AppendTimerEvent(TimerStage stage, TimerAction action, DateTimeOffset at, Guid? actorUserId, long? durationMs = null, TimerStage? from = null, TimerStage? to = null, string? note = null)
        => _timerEvents.Add(new TimerEvent(Guid.NewGuid(), stage, action, at, actorUserId, durationMs, from, to, note));
}
