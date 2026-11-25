using NpiManagement.Domain;

namespace NpiManagement.Application.Npis.Turns;

/// <summary>
/// Lo que hace falta saber de un NPI para decidir a quién le toca (2026-11-03). Se llena desde una
/// proyección SQL (EfNpiTurnReader) sin cargar el agregado completo. "Vigente" = la versión de
/// PrintCard con el número más alto.
/// </summary>
public sealed record TurnFacts(
    LifecycleStatus Lifecycle,
    bool EngineerAssigned,
    bool DesignerAssigned,
    bool EngineeringFeasibility,
    bool DesignFeasibility,
    bool ArtesValidadas,
    bool PendingEngineeringChanges,
    bool PendingDesignChanges,
    bool PendingArtworkRejection,
    bool PendingClientChanges,
    bool PendingCommercialInfo,
    bool SuppliesTemporaryRejection,
    bool HasPrintCard,
    bool PrintCardRejected,
    bool PrintCardEngineeringApproved,
    bool PrintCardClientApproved,
    bool PrintCardSentToClient,
    bool PrintCardLoaded,
    bool PtRegistered,
    bool SapOrderRegistered,
    bool EtCreated,
    bool EtLoaded,
    bool AwaitingSisproQuote,
    bool PendingEngineeringReconfirmation = false,
    bool PendingCommercialEdit = false,
    bool PendingEngineeringInfo = false,
    bool PendingDesignReconfirmation = false);

/// <summary>Qué le toca a un área en un NPI. Action es un código estable; el texto lo pone el frontend.</summary>
public sealed record AreaTurn(AreaType Area, string Action);

/// <summary>
/// "La pelota en tu cancha": qué le toca a cada área en un NPI, con las mismas reglas que deciden qué
/// cronómetro corre en Npi.cs (EngineeringHasPendingWork, CommercialHasPendingWork, el de Diseño).
/// Puede tocarle a más de un área a la vez (Ingeniería y Diseño revisan en paralelo).
/// </summary>
public static class NpiTurns
{
    public const string CompleteDraft = "CompleteDraft";
    public const string AttendEngineeringChanges = "AttendEngineeringChanges";
    public const string AttendDesignChanges = "AttendDesignChanges";
    public const string FinishCommercialEdit = "FinishCommercialEdit";
    public const string UploadNewArtwork = "UploadNewArtwork";
    public const string AnswerInformationRequest = "AnswerInformationRequest";
    public const string ResolveSupplies = "ResolveSupplies";
    public const string QuoteInSispro = "QuoteInSispro";
    public const string SendPrintCardToClient = "SendPrintCardToClient";
    public const string RegisterClientResponse = "RegisterClientResponse";
    public const string RegisterSapOrder = "RegisterSapOrder";
    public const string AssignEngineer = "AssignEngineer";
    public const string ReviewEngineeringFeasibility = "ReviewEngineeringFeasibility";
    public const string ReviewPrintCard = "ReviewPrintCard";
    public const string ReconfirmFeasibility = "ReconfirmFeasibility";
    public const string RegisterPt = "RegisterPt";
    public const string CreateEt = "CreateEt";
    public const string LoadEtInSispro = "LoadEtInSispro";
    public const string AnswerDesignQuestions = "AnswerDesignQuestions";
    public const string AssignDesigner = "AssignDesigner";
    public const string ReviewDesignFeasibility = "ReviewDesignFeasibility";
    public const string ValidateArtwork = "ValidateArtwork";
    public const string MakePrintCard = "MakePrintCard";
    public const string FixPrintCard = "FixPrintCard";
    public const string LoadPrintCard = "LoadPrintCard";
    public const string ReviewClientChange = "ReviewClientChange";

    /// <summary>
    /// Qué otras áreas están detenidas esperando esta acción (2026-11-25): "Ingeniería y Diseño esperan esto".
    /// Con eso se ordena qué hacer primero cuando a alguien le tocan varias cosas en un NPI (lo que destraba a más va arriba).
    /// </summary>
    public static IReadOnlyList<AreaType> WaitingOn(string action, TurnFacts f)
    {
        var waiting = new List<AreaType>();
        void If(bool condition, AreaType area) { if (condition) waiting.Add(area); }
        switch (action)
        {
            case AttendEngineeringChanges:
                waiting.Add(AreaType.Ingenieria);
                If(f.DesignFeasibility && f.ArtesValidadas && !f.PrintCardLoaded, AreaType.Diseno); // el PrintCard también espera
                break;
            case AttendDesignChanges or UploadNewArtwork or AnswerInformationRequest or QuoteInSispro or RegisterClientResponse or AnswerDesignQuestions:
                waiting.Add(AreaType.Diseno);
                break;
            case FinishCommercialEdit:
                If(f.EngineeringFeasibility && !f.EtLoaded, AreaType.Ingenieria);
                If(f.DesignFeasibility && !f.PrintCardLoaded, AreaType.Diseno);
                break;
            case ResolveSupplies:
                // Mientras esté abierto el NPI está detenido: Ingeniería y Diseño no avanzan.
                waiting.Add(AreaType.Ingenieria);
                If(f.DesignerAssigned, AreaType.Diseno);
                break;
            case RegisterSapOrder or MakePrintCard or FixPrintCard or LoadPrintCard or ReviewClientChange:
                waiting.Add(AreaType.Ingenieria);
                break;
            case AssignEngineer or ReviewEngineeringFeasibility:
                If(f.DesignFeasibility, AreaType.Diseno); // el PrintCard necesita la factibilidad de Ingeniería
                break;
            case ReviewPrintCard or ReconfirmFeasibility or RegisterPt:
                waiting.Add(AreaType.Comercial);
                break;
        }
        return waiting;
    }

    /// <summary>
    /// Los turnos de un NPI en el orden en que conviene atenderlos, por área: primero lo que tiene esperando a más
    /// áreas; a igualdad, el orden de siempre. Priority 0 = "Empieza aquí".
    /// </summary>
    public static IReadOnlyList<(AreaTurn Turn, IReadOnlyList<AreaType> WaitingOn, int Priority)> Prioritized(TurnFacts f) =>
        For(f)
            .Select((turn, index) => (turn, waiting: WaitingOn(turn.Action, f), index))
            .GroupBy(x => x.turn.Area)
            .SelectMany(g => g.OrderByDescending(x => x.waiting.Count).ThenBy(x => x.index).Select((x, priority) => (x.turn, x.waiting, priority, x.index)))
            .OrderBy(x => x.index)
            .Select(x => (x.turn, x.waiting, x.priority))
            .ToList();

    /// <summary>Acciones que sólo hace el admin del área (asignar).</summary>
    public static readonly IReadOnlySet<string> AdminOnly = new HashSet<string> { AssignEngineer, AssignDesigner };

    public static IReadOnlyList<AreaTurn> For(TurnFacts f)
    {
        var turns = new List<AreaTurn>();
        void Add(AreaType area, string action) => turns.Add(new AreaTurn(area, action));

        if (f.Lifecycle == LifecycleStatus.Draft)
        {
            Add(AreaType.Comercial, CompleteDraft);
            return turns;
        }
        if (f.Lifecycle != LifecycleStatus.Active) return turns;

        // Cambio del cliente después del PT sin terminar (Npi.ClientChangeUnderReview): no hay pedido ni ET.
        var clientChangeUnderReview = f.PendingDesignReconfirmation
            || (f.PtRegistered && (f.PendingEngineeringReconfirmation || f.PendingCommercialEdit || !f.PrintCardLoaded));
        var printCardWithClient = f.HasPrintCard && !f.PrintCardRejected && f.PrintCardEngineeringApproved && !f.PrintCardClientApproved && !f.PendingClientChanges && !f.PrintCardLoaded;
        var printCardWithEngineering = f.HasPrintCard && !f.PrintCardRejected && !f.PrintCardEngineeringApproved && !f.PendingClientChanges && !f.PrintCardLoaded;

        // ===== Comercial =====
        if (f.PendingEngineeringChanges) Add(AreaType.Comercial, AttendEngineeringChanges);
        if (f.PendingDesignChanges) Add(AreaType.Comercial, AttendDesignChanges);
        if (f.PendingCommercialEdit) Add(AreaType.Comercial, FinishCommercialEdit);
        if (f.PendingArtworkRejection) Add(AreaType.Comercial, UploadNewArtwork);
        if (f.PendingCommercialInfo) Add(AreaType.Comercial, AnswerInformationRequest);
        if (f.SuppliesTemporaryRejection) Add(AreaType.Comercial, ResolveSupplies);
        if (f.AwaitingSisproQuote) Add(AreaType.Comercial, QuoteInSispro);
        if (printCardWithClient) Add(AreaType.Comercial, f.PrintCardSentToClient ? RegisterClientResponse : SendPrintCardToClient);
        if (f.PtRegistered && !f.SapOrderRegistered && !clientChangeUnderReview) Add(AreaType.Comercial, RegisterSapOrder);

        // ===== Ingeniería =====
        // Mientras Comercial atiende los cambios que pidió Ingeniería, su reloj está en pausa (Npi.RequestEngineeringChanges):
        // no le toca ni asignar ni revisar el PrintCard que estaba esperando (NPI-0041, 2026-11-09).
        if (!f.EngineerAssigned) { if (!f.PendingEngineeringChanges) Add(AreaType.Ingenieria, AssignEngineer); }
        else if (!f.EngineeringFeasibility && !f.PendingEngineeringChanges && !f.SuppliesTemporaryRejection) Add(AreaType.Ingenieria, ReviewEngineeringFeasibility);
        if (f.PendingEngineeringReconfirmation && !f.PendingEngineeringChanges) Add(AreaType.Ingenieria, ReconfirmFeasibility);
        if (printCardWithEngineering && !f.PendingEngineeringChanges) Add(AreaType.Ingenieria, ReviewPrintCard);
        if (f.PrintCardLoaded && !f.PtRegistered && !f.PendingEngineeringChanges && !f.PendingEngineeringReconfirmation && !f.PendingCommercialEdit && !f.PendingDesignReconfirmation) Add(AreaType.Ingenieria, RegisterPt);
        if (f.SapOrderRegistered && f.PtRegistered && !f.EtCreated && !clientChangeUnderReview) Add(AreaType.Ingenieria, CreateEt);
        if (f.EtCreated && !f.EtLoaded && !clientChangeUnderReview) Add(AreaType.Ingenieria, LoadEtInSispro);
        if (f.PendingEngineeringInfo) Add(AreaType.Ingenieria, AnswerDesignQuestions);

        // ===== Diseño =====
        // Mientras espera lo que pidió a Comercial o a Ingeniería, su reloj está en pausa: no le toca.
        if (f.DesignerAssigned && f.PendingDesignReconfirmation) Add(AreaType.Diseno, ReviewClientChange);
        if (!f.DesignerAssigned) Add(AreaType.Diseno, AssignDesigner);
        // Rechazo temporal por insumos (2026-11-25): el NPI está detenido; la pelota es de Comercial.
        else if (f.SuppliesTemporaryRejection) { }
        else if (f.PendingCommercialInfo || f.PendingEngineeringInfo) { }
        else if (!f.DesignFeasibility)
        {
            if (!f.PendingDesignChanges) Add(AreaType.Diseno, ReviewDesignFeasibility);
        }
        else if (!f.ArtesValidadas)
        {
            if (!f.PendingArtworkRejection) Add(AreaType.Diseno, ValidateArtwork);
        }
        else if (f.EngineeringFeasibility && !f.AwaitingSisproQuote && !f.PrintCardLoaded && !f.PendingDesignChanges && !f.PendingEngineeringChanges && !f.PendingCommercialEdit)
        {
            if (!f.HasPrintCard) Add(AreaType.Diseno, MakePrintCard);
            else if (f.PrintCardRejected || f.PendingClientChanges) Add(AreaType.Diseno, FixPrintCard);
            else if (f.PrintCardEngineeringApproved && f.PrintCardClientApproved) Add(AreaType.Diseno, LoadPrintCard);
        }

        return turns;
    }
}
