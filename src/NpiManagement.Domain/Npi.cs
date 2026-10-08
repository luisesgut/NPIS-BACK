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

    public DateTimeOffset? PrintCardLoadedAt { get; private set; }
    public Guid? PrintCardLoadedByUserId { get; private set; }
    public Guid? PrintCardLoadedVersionId { get; private set; }

    public DateTimeOffset? PrintCardSentToClientAt { get; private set; }
    public Guid? PrintCardSentToClientByUserId { get; private set; }

    public string? CodigoPT { get; private set; }
    public string? PtName { get; private set; }
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
    private readonly List<TimerEvent> _timerEvents = [];
    private readonly List<AuditEvent> _auditEvents = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<Attachment> Attachments => _attachments;
    public IReadOnlyCollection<ChangeRequest> ChangeRequests => _changeRequests;
    public IReadOnlyCollection<InformationRequest> InformationRequests => _informationRequests;
    public IReadOnlyCollection<FeasibilityReview> FeasibilityReviews => _feasibilityReviews;
    public IReadOnlyCollection<PrintCardVersion> PrintCardVersions => _printCardVersions;
    public IReadOnlyCollection<TimerEvent> TimerEventLog => _timerEvents;
    public IReadOnlyCollection<AuditEvent> AuditLog => _auditEvents;
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    // Por número de versión, nunca por posición: EF Core no garantiza el orden en que carga la
    // colección (las llaves son GUID), así que "la última de la lista" podía ser la v1.
    public PrintCardVersion? CurrentPrintCardVersion => _printCardVersions.MaxBy(v => v.VersionNumber);

    public FeasibilityReview? EngineeringFeasibility => _feasibilityReviews.FirstOrDefault(f => f.Area == AreaType.Ingenieria);
    public FeasibilityReview? DesignFeasibility => _feasibilityReviews.FirstOrDefault(f => f.Area == AreaType.Diseno);

    private Npi(Guid id, Guid createdByUserId, DateTimeOffset createdAt, NpiCaptureData capture, Priority priority, Guid? preAssignedEngineeringUserId)
    {
        Id = id;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        RequestDate = DateOnly.FromDateTime(createdAt.UtcDateTime);
        Capture = capture;
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
        if (LifecycleStatus == Domain.LifecycleStatus.Active && (engineeringChangesPending || designChangesPending))
        {
            if (comment is { Length: > 2000 })
                throw new NpiValidationException("resubmit-comment-too-long", "El comentario no puede exceder 2000 caracteres.");

            if (engineeringChangesPending)
            {
                // Registra qué corrigió Comercial respecto a lo que Ingeniería vio al pedir los cambios.
                PendingEngineeringChangeRequest?.Resolve(actor, now, comment, Capture);
                EngineeringAreaStatus = AssignedEngineeringUserId is not null ? EngineeringAreaStatus.Assigned : EngineeringAreaStatus.Requested;
                if (EngineeringHasPendingWork) ResumeTimer(TimerStage.Engineering, actor, clock, now);
            }

            if (designChangesPending)
            {
                PendingDesignChangeRequest!.Resolve(actor, now, comment, Capture);
                DesignAreaStatus = CurrentDesignAreaStatus();
                ResumeDesignTimer(actor, clock, now);
            }

            if (!CommercialHasPendingWork) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);

            var areas = (engineeringChangesPending, designChangesPending) switch
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

    /// <summary>Acción 4 — Asignar (o reasignar) a Ingeniería.</summary>
    public void AssignEngineering(Guid userId, Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        var now = clock.UtcNow;
        var previous = AssignedEngineeringUserId;
        AssignedEngineeringUserId = userId;
        EngineeringAreaStatus = EngineeringAreaStatus.Assigned;
        Audit(actor, now, previous is null ? "EngineeringAssigned" : "EngineeringReassigned", $"from={previous};to={userId}");
    }

    /// <summary>Acción 5 — Asignar (o reasignar) a Diseño.</summary>
    public void AssignDesign(Guid userId, Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        var now = clock.UtcNow;
        var previous = AssignedDesignUserId;
        AssignedDesignUserId = userId;
        DesignAreaStatus = DesignAreaStatus.Assigned;
        Audit(actor, now, previous is null ? "DesignAssigned" : "DesignReassigned", $"from={previous};to={userId}");
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

        Audit(actor, now, "EngineeringFeasibilityApproved", note);
        _domainEvents.Add(new EngineeringFeasibilityApprovedEvent(Id, now));
    }

    /// <summary>Acción 8 — Factibilidad de Diseño. Independiente de la de Ingeniería (decisión 1).</summary>
    public void ApproveDesignFeasibility(Actor actor, IClock clock, string? note)
    {
        EnsureNotTerminal();
        if (AssignedDesignUserId is null)
            throw new InvalidNpiTransitionException("design-not-assigned", "El NPI no tiene un diseñador asignado.");
        if (DesignFeasibility is not null)
            throw new InvalidNpiTransitionException("design-feasibility-already-approved", "La factibilidad de Diseño ya fue aprobada.");
        if (PendingDesignChangeRequest is not null)
            throw new InvalidNpiTransitionException("design-changes-pending", "Comercial aún no atiende los cambios que pidió Diseño.");

        var now = clock.UtcNow;
        _feasibilityReviews.Add(new FeasibilityReview(Guid.NewGuid(), AreaType.Diseno, actor.UserId, actor.Name, now, note));
        DesignAreaStatus = DesignAreaStatus.FeasibilityApproved;

        Audit(actor, now, "DesignFeasibilityApproved", note);
        _domainEvents.Add(new DesignFeasibilityApprovedEvent(Id, now));
    }

    /// <summary>Acción 9 — Ingeniería rechaza con solicitud de cambios a Comercial (no terminal).</summary>
    public void RequestEngineeringChanges(Actor actor, IClock clock, IBusinessCalendar calendar, string description, IReadOnlyList<string> fieldKeys)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(description) && fieldKeys.Count == 0)
            throw new NpiValidationException("change-request-empty", "Debe incluir una descripción o al menos un campo a modificar.");

        var now = clock.UtcNow;
        _changeRequests.Add(new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Ingenieria, AreaType.Comercial, ChangeRequestOrigin.Internal, description, fieldKeys, ChangeRequest.SnapshotOf(Capture)));
        EngineeringAreaStatus = EngineeringAreaStatus.ChangesRequested;

        EngineeringTimer = EngineeringTimer.Pause(clock, calendar);
        CommercialTimer = CommercialTimer.StartOrResume(clock);
        AppendTimerEvent(TimerStage.Engineering, TimerAction.Pause, now, actor.UserId);
        AppendTimerEvent(TimerStage.Commercial, TimerAction.Resume, now, actor.UserId);

        Audit(actor, now, "EngineeringRequestedChanges", description);
    }

    /// <summary>Acción 10 — Rechazo terminal por no factibilidad (sólo Ingeniería).</summary>
    public void RejectNoFeasibility(Actor actor, IClock clock, IBusinessCalendar calendar, string? note)
    {
        EnsureNotTerminal();
        var now = clock.UtcNow;

        EngineeringAreaStatus = EngineeringAreaStatus.NoFeasibility;
        CloseAllTimers(clock, calendar, now, actor.UserId);
        LifecycleStatus = Domain.LifecycleStatus.Rejected;
        TerminalReason = Domain.TerminalReason.NoFeasibility;

        Audit(actor, now, "NpiRejectedNoFeasibility", note);
        _domainEvents.Add(new NpiRejectedEvent(Id, TerminalReason.Value, now));
    }

    /// <summary>Acción 11 — Artes Validadas (Diseño).</summary>
    public void ValidateArtwork(Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        if (AssignedDesignUserId is null)
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
    /// </summary>
    public void RequestDesignChanges(Actor actor, IClock clock, IBusinessCalendar calendar, string description, IReadOnlyList<string> fieldKeys)
    {
        EnsureActive();
        if (AssignedDesignUserId is null)
            throw new InvalidNpiTransitionException("design-not-assigned", "El NPI no tiene un diseñador asignado.");
        if (DesignFeasibility is not null)
            throw new InvalidNpiTransitionException("design-feasibility-already-approved", "La factibilidad de Diseño ya fue aprobada.");
        if (PendingDesignChangeRequest is not null)
            throw new InvalidNpiTransitionException("design-changes-already-pending", "Ya hay cambios de Diseño pendientes de que Comercial los atienda.");
        RequireRejectionNote(description);

        var now = clock.UtcNow;
        _changeRequests.Add(new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Diseno, AreaType.Comercial, ChangeRequestOrigin.Internal, description.Trim(), fieldKeys, ChangeRequest.SnapshotOf(Capture)));
        DesignAreaStatus = DesignAreaStatus.ChangesRequested;

        PauseTimer(TimerStage.Design, actor, clock, calendar, now);
        RunCommercialTimer(actor, clock, now, "Diseño solicitó cambios");

        Audit(actor, now, "DesignRequestedChanges", description.Trim());
    }

    /// <summary>Rechazo terminal de Diseño por no factibilidad.</summary>
    public void RejectDesignNoFeasibility(Actor actor, IClock clock, IBusinessCalendar calendar, string note)
    {
        EnsureActive();
        if (AssignedDesignUserId is null)
            throw new InvalidNpiTransitionException("design-not-assigned", "El NPI no tiene un diseñador asignado.");
        if (DesignFeasibility is not null)
            throw new InvalidNpiTransitionException("design-feasibility-already-approved", "La factibilidad de Diseño ya fue aprobada.");
        RequireRejectionNote(note);

        var now = clock.UtcNow;
        DesignAreaStatus = DesignAreaStatus.NoFeasibility;
        CloseAllTimers(clock, calendar, now, actor.UserId);
        LifecycleStatus = Domain.LifecycleStatus.Rejected;
        TerminalReason = Domain.TerminalReason.DesignNoFeasibility;

        Audit(actor, now, "NpiRejectedDesignNoFeasibility", note.Trim());
        _domainEvents.Add(new NpiRejectedEvent(Id, TerminalReason.Value, now));
    }

    /// <summary>
    /// Diseño rechaza las artes del cliente (no terminal): Comercial debe subir artes nuevas y
    /// avisar con <see cref="ResolveArtworkRejection"/>. Mientras tanto corre el reloj de Comercial.
    /// </summary>
    public void RejectArtwork(Actor actor, IClock clock, IBusinessCalendar calendar, string reason, Guid? evidenceAttachmentId)
    {
        EnsureActive();
        if (AssignedDesignUserId is null)
            throw new InvalidNpiTransitionException("design-not-assigned", "El NPI no tiene un diseñador asignado.");
        if (ArtesValidadasAt is not null)
            throw new InvalidNpiTransitionException("artwork-already-validated", "Las artes ya fueron validadas.");
        if (PendingArtworkRejection is not null)
            throw new InvalidNpiTransitionException("artwork-rejection-already-pending", "Las artes ya están rechazadas; falta que Comercial suba las corregidas.");
        RequireRejectionNote(reason);
        if (evidenceAttachmentId is { } evidenceId && _attachments.All(a => a.Id != evidenceId))
            throw new NpiValidationException("evidence-not-found", "La evidencia indicada no pertenece a este NPI.");

        var now = clock.UtcNow;
        _changeRequests.Add(new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Diseno, AreaType.Comercial, ChangeRequestOrigin.ArtworkRejected, reason.Trim(), [], evidenceAttachmentId: evidenceAttachmentId));
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
        if (comment is { Length: > 2000 })
            throw new NpiValidationException("resubmit-comment-too-long", "El comentario para Diseño no puede exceder 2000 caracteres.");

        var now = clock.UtcNow;
        pending.Resolve(actor, now, comment, Capture);
        DesignAreaStatus = CurrentDesignAreaStatus();

        if (!CommercialHasPendingWork) PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
        ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "ArtworkRejectionResolved", comment);
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
        || SuppliesTemporaryRejection is not null;

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
    /// Tramos en que la pelota está en la cancha de Ingeniería: antes de su factibilidad, entre
    /// el PrintCard cargado y el PT, y entre el Pedido SAP y la ET en SisPro. Fuera de ellos su
    /// reloj va en pausa (espera a Diseño, al cliente o a Comercial).
    /// </summary>
    private bool EngineeringHasPendingWork =>
        EngineeringFeasibility is null
        || (PrintCardLoadedAt is not null && PtRegisteredAt is null)
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

    /// <summary>Arranca DesignTimer si ambas condiciones (Ing. factible + Artes Validadas) ya se cumplen (invariante 6).</summary>
    private void TryStartDesignTimer(Actor actor, IClock clock, DateTimeOffset now)
    {
        if (EngineeringFeasibility is null || ArtesValidadasAt is null) return;
        if (DesignTimer.HasStarted) return;

        DesignTimer = DesignTimer.StartOrResume(clock);
        AppendTimerEvent(TimerStage.Design, TimerAction.Start, now, actor.UserId);
    }

    /// <summary>Acción 12 — Crear versión de PrintCard. Exige las 4 condiciones de la decisión 2.</summary>
    public void CreatePrintCardVersion(Actor actor, IClock clock, IBusinessCalendar calendar, string code, Guid? attachmentId, string? note)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(code))
            throw new NpiValidationException("printcard-code-required", "El código de PrintCard es obligatorio.");
        if (AssignedDesignUserId is null)
            throw new InvalidNpiTransitionException("design-not-assigned", "PrintCard requiere un diseñador asignado.");
        if (EngineeringFeasibility is null)
            throw new InvalidNpiTransitionException("engineering-feasibility-missing", "PrintCard requiere Factibilidad de Ingeniería aprobada.");
        if (DesignFeasibility is null)
            throw new InvalidNpiTransitionException("design-feasibility-missing", "PrintCard requiere Factibilidad de Diseño aprobada.");
        if (ArtesValidadasAt is null)
            throw new InvalidNpiTransitionException("artwork-not-validated", "PrintCard requiere Artes Validadas.");

        var now = clock.UtcNow;
        var version = new PrintCardVersion(Guid.NewGuid(), _printCardVersions.Count + 1, code, attachmentId, actor.UserId, actor.Name, now, note);
        _printCardVersions.Add(version);
        DesignAreaStatus = DesignAreaStatus.PrintCardInApproval;
        // Cada versión nueva necesita su propio envío al cliente — el envío de una versión
        // anterior no vale para ésta (evita aprobar "en nombre del cliente" algo que nunca vio).
        PrintCardSentToClientAt = null;
        PrintCardSentToClientByUserId = null;

        CommercialTimer = CommercialTimer.StartOrResume(clock);
        DesignTimer = DesignTimer.Pause(clock, calendar);
        AppendTimerEvent(TimerStage.Commercial, TimerAction.Resume, now, actor.UserId);
        AppendTimerEvent(TimerStage.Design, TimerAction.Pause, now, actor.UserId);

        Audit(actor, now, "PrintCardVersionCreated", $"version={version.VersionNumber}");
    }

    /// <summary>Acción 13 — Aprobar la versión vigente de PrintCard bajo un rol. "Client" siempre lo ejecuta Comercial (decisión 8).</summary>
    public void ApprovePrintCard(Actor actor, IClock clock, IBusinessCalendar calendar, ApproverRole role, string? note, Guid? evidenceAttachmentId)
    {
        EnsureNotTerminal();
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard para aprobar.");
        if (version.HasApprovalFrom(role))
            throw new InvalidNpiTransitionException("printcard-already-approved-by-role", $"La versión vigente ya fue aprobada por el rol {role}.");
        if (role == ApproverRole.Client && PrintCardSentToClientAt is null)
            throw new InvalidNpiTransitionException("printcard-not-sent-to-client", "Debes enviar el PrintCard al cliente (\"Enviar a Cliente\") antes de registrar su aprobación.");
        if (role == ApproverRole.Client && evidenceAttachmentId is null)
            throw new NpiValidationException("client-approval-evidence-required", "La aprobación en nombre del Cliente requiere evidencia obligatoria.");

        var now = clock.UtcNow;
        version.AddApproval(Guid.NewGuid(), role, actor.UserId, actor.Name, now, note, evidenceAttachmentId);
        Audit(actor, now, "PrintCardApproved", $"role={role}");

        if (role == ApproverRole.Client)
        {
            CommercialTimer = CommercialTimer.Pause(clock, calendar);
            AppendTimerEvent(TimerStage.Commercial, TimerAction.Pause, now, actor.UserId);
            if (DesignTimer.HasStarted)
            {
                DesignTimer = DesignTimer.StartOrResume(clock);
                AppendTimerEvent(TimerStage.Design, TimerAction.Resume, now, actor.UserId);
            }
        }

        if (version.IsFullyApproved)
        {
            Audit(actor, now, "PrintCardFullyApproved", null);
            _domainEvents.Add(new PrintCardFullyApprovedEvent(Id, version.Id, now));
        }
    }

    /// <summary>Acción 14 — Registrar el envío del PrintCard vigente al cliente (informativo).</summary>
    public void SendPrintCardToClient(Actor actor, IClock clock)
    {
        EnsureNotTerminal();
        _ = CurrentPrintCardVersion ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard para enviar.");

        var now = clock.UtcNow;
        PrintCardSentToClientAt = now;
        PrintCardSentToClientByUserId = actor.UserId;
        Audit(actor, now, "PrintCardSentToClient", null);
    }

    /// <summary>Acción 15 — Cliente solicita cambios (registrado por Comercial, no terminal).</summary>
    public void RequestClientChanges(Actor actor, IClock clock, IBusinessCalendar calendar, string description)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(description))
            throw new NpiValidationException("change-request-empty", "Debe describir los cambios solicitados por el cliente.");
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard vigente.");
        if (version.IsFullyApproved)
            throw new InvalidNpiTransitionException("printcard-already-fully-approved", "La versión vigente ya está totalmente aprobada.");

        var now = clock.UtcNow;
        _changeRequests.Add(new ChangeRequest(Guid.NewGuid(), actor.UserId, actor.Name, now, AreaType.Comercial, AreaType.Diseno, ChangeRequestOrigin.ClientReported, description, []));
        DesignAreaStatus = ArtesValidadasAt is not null ? DesignAreaStatus.ArtesValidadas : DesignAreaStatus.FeasibilityApproved;

        // Rehacer la versión es trabajo de Diseño: antes el reloj seguía corriendo para Comercial
        // (lo había arrancado CreatePrintCardVersion) y Diseño quedaba en pausa.
        PauseTimer(TimerStage.Commercial, actor, clock, calendar, now);
        ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "ClientChangesRequested", description);
    }

    /// <summary>Acción 16 — Rechazo definitivo del cliente (terminal, decisión 7). Siempre lo ejecuta Comercial (decisión 8).</summary>
    public void RejectByClient(Actor actor, IClock clock, IBusinessCalendar calendar, string comment, Guid? evidenceAttachmentId)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(comment))
            throw new NpiValidationException("client-rejection-comment-required", "El rechazo definitivo del cliente exige un comentario.");

        var now = clock.UtcNow;
        ClientRejection = new ClientRejectionInfo(now, actor.UserId, comment, evidenceAttachmentId);
        CloseAllTimers(clock, calendar, now, actor.UserId);
        LifecycleStatus = Domain.LifecycleStatus.Rejected;
        TerminalReason = Domain.TerminalReason.ClientRejection;

        Audit(actor, now, "NpiRejectedByClient", comment);
        _domainEvents.Add(new NpiRejectedEvent(Id, TerminalReason.Value, now));
    }

    /// <summary>Acción 17 — PrintCard cargado en Sistema. NO es terminal; sólo cierra DesignTimer (decisión 3).</summary>
    public void LoadPrintCardIntoSystem(Actor actor, IClock clock, IBusinessCalendar calendar)
    {
        EnsureNotTerminal();
        if (PrintCardLoadedAt is not null)
            throw new InvalidNpiTransitionException("printcard-already-loaded", "El PrintCard ya fue cargado en sistema.");
        var version = CurrentPrintCardVersion
            ?? throw new InvalidNpiTransitionException("no-printcard-version", "No existe una versión de PrintCard vigente.");
        if (!version.HasApprovalFrom(ApproverRole.Client))
            throw new InvalidNpiTransitionException("client-approval-missing", "El PrintCard requiere la aprobación del cliente antes de cargarse en sistema.");

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
    public void RegisterSapPt(Actor actor, IClock clock, IBusinessCalendar calendar, string codigoPT, string? ptName)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(codigoPT))
            throw new NpiValidationException("codigo-pt-required", "El código PT es obligatorio.");
        if (PrintCardLoadedAt is null)
            throw new InvalidNpiTransitionException("printcard-not-loaded", "Debe cargarse el PrintCard en sistema antes de registrar el PT en SAP.");
        if (PtRegisteredAt is not null)
            throw new InvalidNpiTransitionException("pt-already-registered", "El PT ya fue registrado en SAP.");

        var now = clock.UtcNow;
        CodigoPT = codigoPT;
        PtName = ptName;
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
    public void RejectSuppliesTotal(Actor actor, IClock clock, IBusinessCalendar calendar)
    {
        EnsureNotTerminal();
        if (SuppliesTemporaryRejection is null)
            throw new InvalidNpiTransitionException("no-open-supplies-rejection", "No hay un rechazo temporal por insumos abierto.");

        var now = clock.UtcNow;
        CloseAllTimers(clock, calendar, now, actor.UserId);
        LifecycleStatus = Domain.LifecycleStatus.Rejected;
        TerminalReason = Domain.TerminalReason.SuppliesRejectionTotal;

        Audit(actor, now, "NpiRejectedSuppliesTotal", null);
        _domainEvents.Add(new NpiRejectedEvent(Id, TerminalReason.Value, now));
    }

    /// <summary>Acción 25 — Solicitar información (Diseño → Cliente o → Comercial).</summary>
    public void RequestInformation(Actor actor, IClock clock, IBusinessCalendar calendar, InformationRequestType type, string text)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(text))
            throw new NpiValidationException("information-request-text-required", "La solicitud de información requiere texto.");
        if (_informationRequests.Any(r => r.Type == type && r.Status == InformationRequestStatus.Pending))
            throw new InvalidNpiTransitionException("information-request-already-pending", $"Ya existe una solicitud de información pendiente de tipo {type}.");

        var now = clock.UtcNow;
        _informationRequests.Add(new InformationRequest(Guid.NewGuid(), type, text, actor.UserId, actor.Name, now));
        DesignAreaStatus = DesignAreaStatus.AwaitingInformation;

        PauseTimer(TimerStage.Design, actor, clock, calendar, now);

        if (type == InformationRequestType.Commercial)
        {
            CommercialTimer = CommercialTimer.StartOrResume(clock);
            AppendTimerEvent(TimerStage.Commercial, TimerAction.Resume, now, actor.UserId);
        }

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

        ResumeDesignTimer(actor, clock, now);

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
        ResumeDesignTimer(actor, clock, now);

        Audit(actor, now, "CommercialInformationSent", responseText);
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

        Audit(actor, now, "NpiCancelled", reason);
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

    private void Audit(Actor actor, DateTimeOffset at, string action, string? details)
        => _auditEvents.Add(new AuditEvent(Guid.NewGuid(), actor.UserId, actor.Role, at, action, details));

    private void AppendTimerEvent(TimerStage stage, TimerAction action, DateTimeOffset at, Guid? actorUserId, long? durationMs = null, TimerStage? from = null, TimerStage? to = null, string? note = null)
        => _timerEvents.Add(new TimerEvent(Guid.NewGuid(), stage, action, at, actorUserId, durationMs, from, to, note));
}
