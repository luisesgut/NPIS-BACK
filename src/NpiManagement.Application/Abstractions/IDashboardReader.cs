using System.Linq.Expressions;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;

namespace NpiManagement.Application.Abstractions;

/// <summary>Lectura agregada para el tablero de control — implementada en Infrastructure con SQL, nunca cargando agregados completos.</summary>
public interface IDashboardReader
{
    Task<DashboardSummaryDto> GetSummaryAsync(Expression<Func<Npi, bool>> accessPredicate, long slaThresholdBusinessMs, CancellationToken cancellationToken);
}
