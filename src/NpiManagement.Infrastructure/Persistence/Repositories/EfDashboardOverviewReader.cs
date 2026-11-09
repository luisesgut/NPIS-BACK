using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Dashboard;
using NpiManagement.Application.Npis.Turns;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Datos del dashboard por rol (2026-11-09). Trae de SQL sólo lo necesario de los NPIs del alcance
/// (decenas o cientos) y calcula en memoria; lo que "toca" y su espera salen de "Tu turno"
/// (INpiTurnReader), así el dashboard dice lo mismo que los paneles.
/// </summary>
public sealed class EfDashboardOverviewReader(NpiManagementDbContext dbContext, INpiTurnReader turnReader, IClock clock) : IDashboardOverviewReader
{
    /// <summary>Jornada hábil de MexicoBusinessCalendar: 09:00–18:00.</summary>
    private const double BusinessDayMs = 9 * 60 * 60 * 1000;
    private static readonly TimeSpan MexicoOffset = TimeSpan.FromHours(-6);

    /// <summary>Lo que se cuenta de la bitácora en "qué hizo el área".</summary>
    private static readonly string[] ActivityActions =
    [
        "NpiSubmitted", "EngineeringFeasibilityApproved", "EngineeringRequestedChanges", "PrintCardRejectedByEngineering",
        "NpiRejectedNoFeasibility", "DesignFeasibilityApproved", "DesignRequestedChanges", "ArtworkRejected", "PrintCardVersionCreated",
        "ClientChangesRequested", "PrintCardRetiredByDesign", "PrintCardRetiredByEngineering", "PrintCardRetiredByCommercial",
        "NpiRejectedDesignNoFeasibility", "NpiRejectedByPrice", "NpiRejectedByClient", "NpiRejectedSuppliesTotal", "NpiCancelled",
    ];

    public async Task<DashboardOverviewDto> GetAsync(
        DashboardView view, AreaType? area, Expression<Func<Npi, bool>> accessPredicate, DashboardFilter filter, DashboardGoals goals, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var (from, previousFrom) = PeriodStart(now, filter.Months);

        var query = dbContext.Npis.AsNoTracking().Where(accessPredicate).Where(n => n.LifecycleStatus != LifecycleStatus.Draft);
        if (!string.IsNullOrWhiteSpace(filter.Cliente)) query = query.Where(n => n.Capture.Cliente == filter.Cliente.Trim());
        if (Enum.TryParse<Priority>(filter.Priority, true, out var priority)) query = query.Where(n => n.Priority == priority);

        var npis = await query.Select(n => new
        {
            n.Id,
            n.LifecycleStatus,
            n.TerminalReason,
            ClosedAt = n.GlobalTimer.EndAt,
            GlobalMs = n.GlobalTimer.AccumulatedBusinessMs,
            CommercialMs = n.CommercialTimer.AccumulatedBusinessMs,
            EngineeringMs = n.EngineeringTimer.AccumulatedBusinessMs,
            DesignMs = n.DesignTimer.AccumulatedBusinessMs,
            n.AssignedCommercialUserId,
            n.AssignedEngineeringUserId,
            n.AssignedDesignUserId,
        }).ToListAsync(cancellationToken);
        var ids = npis.Select(n => n.Id).ToHashSet();
        double Days(long ms) => Math.Round(ms / BusinessDayMs, 1);

        // ===== Lo que toca hoy (de "Tu turno"), con su meta =====
        var turns = (await turnReader.ListAsync(accessPredicate, cancellationToken))
            .Where(t => ids.Contains(t.NpiId))
            .Where(t => area is null || t.Area == area.ToString())
            .Where(t => view != DashboardView.Personal || !NpiTurns.AdminOnly.Contains(t.Action))
            .Select(t =>
            {
                var goal = goals.ForAction(t.Action);
                double? days = t.WaitingBusinessMs is { } ms ? Days(ms) : null;
                return new QueueItemDto(t.NpiId, t.NpiNumber, t.Cliente, t.DescripcionProducto, t.Area, t.Action, goal?.Stage ?? "otra",
                    days, goal?.Days, goal is { } g && days > g.Days, t.AssigneeName);
            })
            .ToList();
        var lateNpis = turns.Where(t => t.Late).Select(t => t.NpiId).ToHashSet();
        var stages = turns.GroupBy(t => t.Stage)
            .Select(g => new StageLoadDto(g.Key, g.Select(t => t.NpiId).Distinct().Count(), g.Where(t => t.Late).Select(t => t.NpiId).Distinct().Count()))
            .ToList();
        // Dirección ve lo atrasado; un área (o una persona) ve todo lo suyo, lo más viejo primero.
        var queue = (view == DashboardView.Director ? turns.Where(t => t.Late) : turns)
            .OrderByDescending(t => t.Late).ThenByDescending(t => t.Days ?? -1).ToList();

        // ===== Periodo: terminados, ciclo, a tiempo, motivos =====
        bool In(DateTimeOffset? at, DateTimeOffset start, DateTimeOffset end) => at is { } a && a >= start && a < end;
        var completed = npis.Where(n => n.LifecycleStatus == LifecycleStatus.Completed).ToList();
        var lost = npis.Where(n => n.LifecycleStatus is LifecycleStatus.Rejected or LifecycleStatus.Cancelled).ToList();
        var doneNow = completed.Where(n => In(n.ClosedAt, from, now)).ToList();
        var donePrev = completed.Where(n => In(n.ClosedAt, previousFrom, from)).ToList();
        double? AvgCycle(IReadOnlyCollection<long> ms) => ms.Count == 0 ? null : Math.Round(ms.Average() / BusinessDayMs, 1);
        double? OnTime(IReadOnlyCollection<long> ms) => ms.Count == 0 ? null : Math.Round(100.0 * ms.Count(m => m / BusinessDayMs <= goals.CicloCompleto) / ms.Count);
        var nowMs = doneNow.Select(n => n.GlobalMs).ToList();
        var prevMs = donePrev.Select(n => n.GlobalMs).ToList();

        var losses = lost.Where(n => In(n.ClosedAt, from, now))
            .GroupBy(n => n.LifecycleStatus == LifecycleStatus.Cancelled ? "Cancelled" : n.TerminalReason?.ToString() ?? "Otro")
            .Select(g => new CountDto(g.Key, g.Count())).OrderByDescending(c => c.Count).ToList();

        var local = now.ToOffset(MexicoOffset);
        var trend = Enumerable.Range(0, 6).Reverse().Select(back =>
        {
            var month = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, MexicoOffset).AddMonths(-back);
            var next = month.AddMonths(1);
            var done = completed.Where(n => In(n.ClosedAt, month, next)).Select(n => n.GlobalMs).ToList();
            return new MonthTrendDto(month.Year, month.Month, done.Count, lost.Count(n => In(n.ClosedAt, month, next)), AvgCycle(done));
        }).ToList();

        // ===== Tiempo por área (terminados del periodo) =====
        var areaTimes = new List<AreaTimeDto>
        {
            new("Comercial", doneNow.Count == 0 ? null : Days((long)doneNow.Average(n => n.CommercialMs)), goals.AreaComercial, turns.Count(t => t.Area == "Comercial")),
            new("Ingenieria", doneNow.Count == 0 ? null : Days((long)doneNow.Average(n => n.EngineeringMs)), goals.AreaIngenieria, turns.Count(t => t.Area == "Ingenieria")),
            new("Diseno", doneNow.Count == 0 ? null : Days((long)doneNow.Average(n => n.DesignMs)), goals.AreaDiseno, turns.Count(t => t.Area == "Diseno")),
        };

        // ===== Carga por persona del área (sólo vista de área) =====
        var load = new List<PersonLoadDto>();
        if (view == DashboardView.Area && area is not null)
        {
            var byOwner = npis.Where(n => n.LifecycleStatus == LifecycleStatus.Active)
                .Select(n => new
                {
                    n.Id,
                    Owner = area switch
                    {
                        AreaType.Comercial => (Guid?)n.AssignedCommercialUserId,
                        AreaType.Ingenieria => n.AssignedEngineeringUserId,
                        _ => n.AssignedDesignUserId,
                    },
                })
                .Where(x => x.Owner is not null).GroupBy(x => x.Owner!.Value).ToList();
            var ownerIds = byOwner.Select(g => g.Key).ToList();
            var names = await dbContext.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
            load = byOwner
                .Select(g => new PersonLoadDto(g.Key, names.GetValueOrDefault(g.Key) ?? "Usuario", g.Count(), g.Count(x => lateNpis.Contains(x.Id))))
                .OrderByDescending(p => p.Active).ToList();
        }

        // ===== Qué hizo el área: bitácora del periodo y campos más corregidos =====
        var idList = ids.ToList();
        var activity = await dbContext.Set<AuditEvent>().AsNoTracking()
            .Where(e => idList.Contains(EF.Property<Guid>(e, "NpiId")) && e.OccurredAt >= from && ActivityActions.Contains(e.Action))
            .GroupBy(e => e.Action).Select(g => new CountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var correctionAreas = area switch
        {
            AreaType.Ingenieria => new[] { AreaType.Ingenieria },
            AreaType.Diseno => new[] { AreaType.Diseno },
            _ => new[] { AreaType.Ingenieria, AreaType.Diseno },
        };
        var correctedKeys = await dbContext.Set<ChangeRequest>().AsNoTracking()
            .Where(c => idList.Contains(EF.Property<Guid>(c, "NpiId")) && c.RequestedAt >= from
                && c.ToArea == AreaType.Comercial && c.Origin == ChangeRequestOrigin.Internal && correctionAreas.Contains(c.FromArea))
            .Select(c => c.FieldKeys)
            .ToListAsync(cancellationToken);
        var topFields = correctedKeys.SelectMany(k => k).GroupBy(k => k)
            .Select(g => new CountDto(g.Key, g.Count())).OrderByDescending(c => c.Count).Take(6).ToList();

        return new DashboardOverviewDto(
            view.ToString(), area?.ToString(), new PeriodDto(from, now, previousFrom), goals,
            npis.Count(n => n.LifecycleStatus == LifecycleStatus.Active),
            new ComparedDto(doneNow.Count, donePrev.Count),
            new ComparedDto(AvgCycle(nowMs), AvgCycle(prevMs)),
            new ComparedDto(OnTime(nowMs), OnTime(prevMs)),
            turns.Select(t => t.NpiId).Distinct().Count(),
            lateNpis.Count,
            stages, queue, trend, losses, areaTimes, load, activity, topFields);
    }

    /// <summary>Inicio del periodo (mes, trimestre o año en curso, hora de México) y del periodo anterior.</summary>
    private static (DateTimeOffset From, DateTimeOffset PreviousFrom) PeriodStart(DateTimeOffset now, int months)
    {
        var local = now.ToOffset(MexicoOffset);
        var startMonth = months switch
        {
            12 => 1,
            3 => (local.Month - 1) / 3 * 3 + 1,
            _ => local.Month,
        };
        var from = new DateTimeOffset(local.Year, startMonth, 1, 0, 0, 0, MexicoOffset);
        return (from, from.AddMonths(-months));
    }
}
