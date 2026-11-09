namespace NpiManagement.Application.Npis.Queries;

/// <summary>
/// Métricas agregadas para los tableros de control (Fase 7, docs/api-contract.md §9). Todo se
/// calcula con agregaciones en SQL sobre el subconjunto de NPIs visible para el actor (mismo
/// predicado de autorización que ListNpisQuery) — nunca se materializan agregados completos para
/// contarlos en memoria.
/// </summary>
public sealed record DashboardSummaryDto(
    int TotalCount,
    IReadOnlyDictionary<string, int> CountsByLifecycleStatus,
    int ClosedCount,
    int InProcessCount,
    IReadOnlyDictionary<string, int> ClosedCountByPriority,
    IReadOnlyDictionary<string, int> InProcessCountByPriority,
    double? AverageGlobalDurationBusinessMs,
    IReadOnlyDictionary<string, double?> AverageGlobalDurationBusinessMsByPriority,
    double? AverageEngineeringDurationBusinessMs,
    double SlaCompliancePercent,
    IReadOnlyDictionary<string, int> CountsByClient,
    double? AverageCommercialDurationBusinessMs,
    double? AverageDesignDurationBusinessMs);
