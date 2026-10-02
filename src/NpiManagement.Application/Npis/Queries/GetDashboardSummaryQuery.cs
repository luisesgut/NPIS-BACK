using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;

namespace NpiManagement.Application.Npis.Queries;

public sealed record GetDashboardSummaryQuery(CurrentUser Actor);

public sealed class GetDashboardSummaryQueryHandler(IDashboardReader reader)
{
    /// <summary>
    /// Umbral de cumplimiento de SLA: 9 días hábiles (criterio de negocio ya usado como
    /// referencia informal por Comercial). El calendario hábil (MexicoBusinessCalendar) define
    /// la jornada como 09:00–18:00 = 9 horas hábiles/día, de ahí 9 * 9h en milisegundos.
    /// </summary>
    private const long SlaThresholdBusinessMs = 9L * 9 * 60 * 60 * 1000;

    public Task<DashboardSummaryDto> HandleAsync(GetDashboardSummaryQuery query, CancellationToken cancellationToken)
    {
        var accessPredicate = NpiAuthorization.BuildAccessPredicate(query.Actor);
        return reader.GetSummaryAsync(accessPredicate, SlaThresholdBusinessMs, cancellationToken);
    }
}
