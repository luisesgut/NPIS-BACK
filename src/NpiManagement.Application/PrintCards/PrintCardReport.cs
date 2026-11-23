
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Dashboard;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.PrintCards;

// =====================================================================================================
// Módulo "PrintCards" (2026-11-23, v1.2.0): historial de PrintCards, indicadores por diseñador y la
// producción del área. Pedido del admin de Diseño. Sólo lectura: todo sale de lo que el NPI ya guarda
// (versiones y aprobaciones, bitácora y eventos del cronómetro de Diseño); no lleva migración.
//
// Definiciones (acordadas con Luis):
// - Un "PrintCard" es el de un NPI; sus versiones son el detalle (retrabajo).
// - Enviado = el PrintCard salió al cliente (primer "Enviar a Cliente"). Las versiones rechazadas por
//   Ingeniería no cuentan como enviadas.
// - Tiempo activo = horas hábiles que corrió el cronómetro de Diseño hasta ese primer envío (no cuenta
//   cuando esperaba a Comercial, al cliente o a Ingeniería). Es el número para comparar diseñadores.
// - Días naturales = de la asignación del diseñador al primer envío. Es lo que vive el cliente.
// - Salió a la primera = una sola versión y el cliente no pidió cambios.
// =====================================================================================================

/// <summary>Una versión del PrintCard con lo que le pasó.</summary>
/// <param name="Outcome">en_revision · aprobada_ing · enviada · cambios_cliente · aprobada_cliente · rechazada_ing · retirada.</param>
public sealed record PrintCardVersionRowDto(
    int VersionNumber, string Code, string UploadedByName, DateTimeOffset UploadedAt, string Outcome, string? RejectionReason,
    DateTimeOffset? EngineeringApprovedAt, DateTimeOffset? SentAt, DateTimeOffset? ClientApprovedAt);

/// <summary>El PrintCard de un NPI, con sus tiempos y su historial de versiones.</summary>
/// <param name="Status">en_proceso · esperando_cliente · cambios_cliente · aprobado · cancelado.</param>
public sealed record PrintCardRecordDto(
    Guid NpiId, string? NpiNumber, string Cliente, string DescripcionProducto,
    Guid? DesignerUserId, string DesignerName, string CurrentCode, int Versions, int EngineeringRejections, int ClientChangeRounds,
    DateTimeOffset FirstUploadedAt, DateTimeOffset? FirstSentAt, double? ActiveHours, double? CalendarDays,
    string Status, bool FirstPass, IReadOnlyList<PrintCardVersionRowDto> History);

public sealed record DesignerStatsDto(
    Guid? DesignerUserId, string Name, int Sent, double? AvgActiveHours, double? AvgCalendarDays,
    double? VersionsPerPrintCard, double? FirstPassPercent, int InProgress);

public sealed record PrintCardMonthDto(int Year, int Month, int FirstPass, int Rework);

public sealed record PrintCardReportDto(
    PeriodDto? Period,
    /// <summary>Meta de horas activas por PrintCard (meta "GenerarPrintCard" del dashboard × 9 h hábiles).</summary>
    double GoalHours,
    /// <summary>El diseñador sólo ve lo suyo.</summary>
    bool OnlyMine,
    ComparedDto Sent,
    ComparedDto AvgActiveHours,
    ComparedDto AvgCalendarDays,
    ComparedDto FirstPassPercent,
    int InProgress,
    IReadOnlyList<CountDto> ByStatus,
    IReadOnlyList<CountDto> ByClient,
    IReadOnlyList<PrintCardMonthDto> ByMonth,
    IReadOnlyList<DesignerStatsDto> Designers,
    /// <summary>Historial: enviados en el periodo y todos los que siguen en proceso, lo más reciente primero.</summary>
    IReadOnlyList<PrintCardRecordDto> Records);

// ===== Datos crudos que entrega Infrastructure =====

public sealed record PrintCardApprovalSource(ApproverRole Role, DateTimeOffset At);

public sealed record PrintCardVersionSource(
    int VersionNumber, string Code, Guid UploadedByUserId, string UploadedByName, DateTimeOffset UploadedAt,
    DateTimeOffset? RejectedAt, string? RejectionReason, IReadOnlyList<PrintCardApprovalSource> Approvals);

public sealed record PrintCardEventSource(DateTimeOffset At, string Action, string? Details);

public sealed record PrintCardTimerEventSource(DateTimeOffset At, TimerAction Action);

public sealed record PrintCardNpiSource(
    Guid NpiId, string? NpiNumber, string Cliente, string DescripcionProducto, LifecycleStatus Lifecycle, Guid? AssignedDesignUserId,
    IReadOnlyList<PrintCardVersionSource> Versions,
    /// <summary>Bitácora: DesignAssigned/Reassigned, PrintCardSentToClient, ClientChangesRequested, ActionUndone.</summary>
    IReadOnlyList<PrintCardEventSource> Events,
    /// <summary>Eventos del cronómetro de Diseño.</summary>
    IReadOnlyList<PrintCardTimerEventSource> DesignTimer);

public interface IPrintCardReportReader
{
    /// <summary>Todos los NPIs (sin borradores) con al menos una versión de PrintCard; el handler filtra por diseñador.</summary>
    Task<IReadOnlyList<PrintCardNpiSource>> ListAsync(CancellationToken cancellationToken);
}

public sealed record PrintCardExportFile(byte[] Content, string ContentType, string FileName);

public interface IPrintCardReportExporter
{
    PrintCardExportFile Build(IReadOnlyList<PrintCardRecordDto> records);
}

/// <summary>Cálculo puro de un PrintCard a partir de sus datos crudos (probado en Application.Tests).</summary>
public static class PrintCardMetrics
{
    /// <summary>Motivos con los que el sistema "retira" una versión para rehacerla (no son rechazo de Ingeniería).</summary>
    private static readonly HashSet<string> RetireReasons =
    [
        Npi.PrintCardRetiredByDesignReason, Npi.PrintCardRetiredByEngineeringReason,
        Npi.PrintCardRetiredByCommercialReason, Npi.PrintCardRetiredByClientChangeReason,
    ];

    public static PrintCardRecordDto Build(PrintCardNpiSource npi, IReadOnlyDictionary<Guid, string> names, IBusinessCalendar calendar, DateTimeOffset now)
    {
        var versions = npi.Versions.OrderBy(v => v.VersionNumber).ToList();
        var events = npi.Events.OrderBy(e => e.At).ToList();

        // Envíos y cambios del cliente, descontando los que se deshicieron ("ActionUndone" quita el último).
        var sends = new List<DateTimeOffset>();
        var clientChanges = new List<DateTimeOffset>();
        DateTimeOffset? assignedAt = null;
        foreach (var e in events)
        {
            switch (e.Action)
            {
                case "PrintCardSentToClient": sends.Add(e.At); break;
                case "ClientChangesRequested": clientChanges.Add(e.At); break;
                case "DesignAssigned": assignedAt ??= e.At; break;
                case "ActionUndone" when e.Details == "PrintCardSentToClient" && sends.Count > 0: sends.RemoveAt(sends.Count - 1); break;
                case "ActionUndone" when e.Details == "ClientChangesRequested" && clientChanges.Count > 0: clientChanges.RemoveAt(clientChanges.Count - 1); break;
            }
        }

        // Cada envío / cambio del cliente es de la versión vigente en ese momento.
        PrintCardVersionSource? VersionAt(DateTimeOffset at) => versions.LastOrDefault(v => v.UploadedAt <= at);
        var history = versions.Select(v =>
        {
            var sentAt = sends.Where(s => VersionAt(s) == v).Select(s => (DateTimeOffset?)s).FirstOrDefault();
            var changed = clientChanges.Any(c => VersionAt(c) == v);
            var ingApproved = v.Approvals.FirstOrDefault(a => a.Role == ApproverRole.Engineering)?.At;
            var clientApproved = v.Approvals.FirstOrDefault(a => a.Role == ApproverRole.Client)?.At;
            var outcome =
                v.RejectedAt is not null ? (v.RejectionReason is { } r && RetireReasons.Contains(r) ? "retirada" : "rechazada_ing")
                : clientApproved is not null ? "aprobada_cliente"
                : changed ? "cambios_cliente"
                : sentAt is not null ? "enviada"
                : ingApproved is not null ? "aprobada_ing"
                : "en_revision";
            return new PrintCardVersionRowDto(v.VersionNumber, v.Code, v.UploadedByName, v.UploadedAt, outcome,
                outcome == "rechazada_ing" ? v.RejectionReason : null, ingApproved, sentAt, clientApproved);
        }).ToList();

        var current = versions[^1];
        var currentRow = history[^1];
        var firstSent = sends.Count > 0 ? sends[0] : (DateTimeOffset?)null;
        var status = currentRow.Outcome switch
        {
            "aprobada_cliente" => "aprobado",
            _ when npi.Lifecycle is LifecycleStatus.Rejected or LifecycleStatus.Cancelled => "cancelado",
            "enviada" => "esperando_cliente",
            "cambios_cliente" => "cambios_cliente",
            // Rehaciendo después de que el cliente pidió cambios a una versión anterior.
            _ when clientChanges.Count > 0 => "cambios_cliente",
            _ => "en_proceso",
        };

        var activeMs = ActiveDesignMs(npi.DesignTimer, firstSent ?? now, calendar);
        var start = assignedAt ?? npi.DesignTimer.OrderBy(t => t.At).FirstOrDefault()?.At;
        double? calendarDays = firstSent is { } sent && start is { } s ? Math.Round(Math.Max(0, (sent - s).TotalDays), 1) : null;

        var designerId = npi.AssignedDesignUserId ?? current.UploadedByUserId;
        var designerName = names.TryGetValue(designerId, out var n) ? n : current.UploadedByName;

        return new PrintCardRecordDto(
            npi.NpiId, npi.NpiNumber, npi.Cliente, npi.DescripcionProducto, designerId, designerName, current.Code, versions.Count,
            history.Count(h => h.Outcome == "rechazada_ing"), clientChanges.Count, versions[0].UploadedAt, firstSent,
            activeMs is { } ms ? Math.Round(ms / 3_600_000.0, 1) : null, calendarDays, status,
            FirstPass: versions.Count == 1 && clientChanges.Count == 0, history);
    }

    /// <summary>Horas hábiles que corrió el cronómetro de Diseño hasta <paramref name="until"/>. null si nunca arrancó.</summary>
    public static long? ActiveDesignMs(IReadOnlyList<PrintCardTimerEventSource> timer, DateTimeOffset until, IBusinessCalendar calendar)
    {
        var ordered = timer.OrderBy(t => t.At).ToList();
        if (ordered.Count == 0) return null;
        long total = 0;
        DateTimeOffset? runningSince = null;
        foreach (var t in ordered)
        {
            if (t.At > until) break;
            if (t.Action is TimerAction.Start or TimerAction.Resume) runningSince ??= t.At;
            else if (t.Action is TimerAction.Pause or TimerAction.Close && runningSince is { } since)
            {
                total += calendar.BusinessMillisecondsBetween(since, t.At);
                runningSince = null;
            }
        }
        if (runningSince is { } open) total += calendar.BusinessMillisecondsBetween(open, until);
        return total;
    }
}

/// <param name="Months">1, 3, 6 o 12 meses; 0 = todo.</param>
public sealed record PrintCardReportQuery(CurrentUser Actor, int Months, string? Cliente, Guid? DesignerUserId);

/// <summary>
/// Quién ve qué: admin de Diseño, AdminTotal y Dirección ven a todos los diseñadores (pueden filtrar
/// por uno); un diseñador sólo ve sus PrintCards. El resto de los roles no entra (política de la API).
/// </summary>
public sealed class PrintCardReportQueryHandler(IPrintCardReportReader reader, IUserDirectory users, IBusinessCalendar calendar, IClock clock, DashboardGoals goals)
{
    public static readonly int[] AllowedMonths = [0, 1, 3, 6, 12];

    public async Task<PrintCardReportDto> HandleAsync(PrintCardReportQuery query, CancellationToken cancellationToken)
    {
        var (records, onlyMine) = await RecordsAsync(query, cancellationToken);
        var now = clock.UtcNow;
        PeriodDto? period = null;
        DateTimeOffset? from = null, previousFrom = null;
        if (query.Months > 0)
        {
            from = now.AddMonths(-query.Months);
            previousFrom = from.Value.AddMonths(-query.Months);
            period = new PeriodDto(from.Value, now, previousFrom.Value);
        }

        bool InPeriod(PrintCardRecordDto r) => r.FirstSentAt is { } s && (from is null || s >= from);
        bool InPrevious(PrintCardRecordDto r) => r.FirstSentAt is { } s && from is not null && s >= previousFrom && s < from;
        var sent = records.Where(InPeriod).ToList();
        var previous = records.Where(InPrevious).ToList();
        var inProgress = records.Where(r => r.FirstSentAt is null && r.Status != "cancelado").ToList();

        static double? Avg(IEnumerable<double?> values) { var list = values.OfType<double>().ToList(); return list.Count == 0 ? null : Math.Round(list.Average(), 1); }
        static double? FirstPass(IReadOnlyCollection<PrintCardRecordDto> rs) => rs.Count == 0 ? null : Math.Round(100.0 * rs.Count(r => r.FirstPass) / rs.Count);
        ComparedDto Compare(Func<List<PrintCardRecordDto>, double?> f) => new(f(sent), from is null ? null : f(previous));

        var designers = records.GroupBy(r => (r.DesignerUserId, r.DesignerName))
            .Select(g =>
            {
                var gs = g.Where(InPeriod).ToList();
                return new DesignerStatsDto(g.Key.DesignerUserId, g.Key.DesignerName, gs.Count,
                    Avg(gs.Select(r => r.ActiveHours)), Avg(gs.Select(r => r.CalendarDays)),
                    gs.Count == 0 ? null : Math.Round(gs.Average(r => r.Versions), 1), FirstPass(gs),
                    g.Count(r => r.FirstSentAt is null && r.Status != "cancelado"));
            })
            .Where(d => d.Sent > 0 || d.InProgress > 0)
            .OrderByDescending(d => d.Sent).ThenBy(d => d.Name)
            .ToList();

        var byMonth = sent.GroupBy(r => { var local = r.FirstSentAt!.Value.ToOffset(TimeSpan.FromHours(-6)); return (local.Year, local.Month); })
            .OrderBy(g => g.Key)
            .Select(g => new PrintCardMonthDto(g.Key.Year, g.Key.Month, g.Count(r => r.FirstPass), g.Count(r => !r.FirstPass)))
            .ToList();

        return new PrintCardReportDto(
            period, goals.GenerarPrintCard * 9, onlyMine,
            Compare(rs => rs.Count), Compare(rs => Avg(rs.Select(r => r.ActiveHours))), Compare(rs => Avg(rs.Select(r => r.CalendarDays))), Compare(rs => FirstPass(rs)),
            inProgress.Count,
            sent.GroupBy(r => r.Status).Select(g => new CountDto(g.Key, g.Count())).OrderByDescending(c => c.Count).ToList(),
            sent.GroupBy(r => r.Cliente).Select(g => new CountDto(g.Key, g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Key).Take(8).ToList(),
            byMonth, designers,
            sent.Concat(inProgress).OrderByDescending(r => r.FirstSentAt ?? r.FirstUploadedAt).ToList());
    }

    /// <summary>Historial para Excel: los mismos registros que ve en pantalla con esos filtros.</summary>
    public async Task<IReadOnlyList<PrintCardRecordDto>> ExportRecordsAsync(PrintCardReportQuery query, CancellationToken cancellationToken) =>
        (await HandleAsync(query, cancellationToken)).Records;

    private async Task<(List<PrintCardRecordDto> Records, bool OnlyMine)> RecordsAsync(PrintCardReportQuery query, CancellationToken cancellationToken)
    {
        if (!AllowedMonths.Contains(query.Months))
            throw new RequestValidationException("printcards-period", "El periodo debe ser 1, 3, 6 o 12 meses, o todo.");

        var actor = query.Actor;
        var onlyMine = actor.Role == ApplicationRoles.UsuarioDiseno;
        var designer = onlyMine ? actor.UserId : query.DesignerUserId;

        // Admins y Dirección ven todos los NPIs; el diseñador, todos los que tienen PrintCard suyo (aunque
        // ya no esté en su equipo): se filtra abajo por diseñador, no por el alcance de los paneles.
        var sources = await reader.ListAsync(cancellationToken);
        var names = await users.GetNamesAsync(sources.Select(s => s.AssignedDesignUserId).OfType<Guid>().Distinct().ToList(), cancellationToken);
        var now = clock.UtcNow;
        var records = sources.Select(s => PrintCardMetrics.Build(s, names, calendar, now))
            .Where(r => designer is null || r.DesignerUserId == designer)
            .Where(r => string.IsNullOrWhiteSpace(query.Cliente) || string.Equals(r.Cliente, query.Cliente.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        return (records, onlyMine);
    }
}
