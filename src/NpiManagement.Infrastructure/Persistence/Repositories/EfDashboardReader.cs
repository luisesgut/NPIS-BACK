using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Métricas del tablero de control (Fase 7) calculadas con GroupBy/Count/Average en SQL sobre el
/// subconjunto autorizado — nunca se traen agregados Npi completos a memoria para contarlos
/// (mismo principio que EfNpiListReader, ver docs/authorization-matrix.md §0).
/// </summary>
public sealed class EfDashboardReader(NpiManagementDbContext dbContext) : IDashboardReader
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(Expression<Func<Npi, bool>> accessPredicate, long slaThresholdBusinessMs, CancellationToken cancellationToken)
    {
        var query = dbContext.Npis.AsNoTracking().Where(accessPredicate);

        var totalCount = await query.CountAsync(cancellationToken);

        var lifecycleRows = await query
            .GroupBy(n => n.LifecycleStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var countsByLifecycleStatus = lifecycleRows.ToDictionary(x => x.Status.ToString(), x => x.Count);

        var closedQuery = query.Where(n => n.GlobalTimer.EndAt != null);
        var inProcessQuery = query.Where(n => n.GlobalTimer.EndAt == null);

        var closedByPriority = await closedQuery
            .GroupBy(n => n.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count(), AvgMs = g.Average(n => (double)n.GlobalTimer.AccumulatedBusinessMs) })
            .ToListAsync(cancellationToken);
        var inProcessByPriority = await inProcessQuery
            .GroupBy(n => n.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var closedCount = closedByPriority.Sum(x => x.Count);
        var inProcessCount = totalCount - closedCount;

        var closedCountByPriority = ToPriorityCountDictionary(closedByPriority.Select(x => (x.Priority, x.Count)));
        var inProcessCountByPriority = ToPriorityCountDictionary(inProcessByPriority.Select(x => (x.Priority, x.Count)));
        var averageGlobalDurationBusinessMsByPriority = Enum.GetValues<Priority>()
            .ToDictionary(p => p.ToString(), p => (double?)closedByPriority.FirstOrDefault(x => x.Priority == p)?.AvgMs);

        double? averageGlobalDurationBusinessMs = closedCount > 0
            ? closedByPriority.Sum(x => x.AvgMs * x.Count) / closedCount
            : null;

        var engineeringClosedQuery = query.Where(n => n.EngineeringTimer.EndAt != null);
        var engineeringClosedCount = await engineeringClosedQuery.CountAsync(cancellationToken);
        double? averageEngineeringDurationBusinessMs = engineeringClosedCount > 0
            ? await engineeringClosedQuery.AverageAsync(n => (double)n.EngineeringTimer.AccumulatedBusinessMs, cancellationToken)
            : null;

        var withinSlaCount = closedCount > 0
            ? await closedQuery.CountAsync(n => n.GlobalTimer.AccumulatedBusinessMs <= slaThresholdBusinessMs, cancellationToken)
            : 0;
        var slaCompliancePercent = closedCount > 0 ? Math.Round(100.0 * withinSlaCount / closedCount, 1) : 100.0;

        var topClients = await query
            .Where(n => n.Capture.Cliente != "")
            .GroupBy(n => n.Capture.Cliente)
            .Select(g => new { Client = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync(cancellationToken);
        var countsByClient = topClients.ToDictionary(x => x.Client, x => x.Count);

        return new DashboardSummaryDto(
            totalCount,
            countsByLifecycleStatus,
            closedCount,
            inProcessCount,
            closedCountByPriority,
            inProcessCountByPriority,
            averageGlobalDurationBusinessMs,
            averageGlobalDurationBusinessMsByPriority,
            averageEngineeringDurationBusinessMs,
            slaCompliancePercent,
            countsByClient);
    }

    private static Dictionary<string, int> ToPriorityCountDictionary(IEnumerable<(Priority Priority, int Count)> rows)
    {
        var dict = Enum.GetValues<Priority>().ToDictionary(p => p.ToString(), _ => 0);
        foreach (var (priority, count) in rows) dict[priority.ToString()] = count;
        return dict;
    }
}
