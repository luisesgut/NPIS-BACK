using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Genera folios de NPI de forma atómica y segura bajo concurrencia. La implementación de
/// Infrastructure usa un SEQUENCE de SQL Server (ver docs/architecture-decisions.md ADR-005) —
/// nunca "consultar máximo + 1" sin protección.
/// </summary>
public interface INpiNumberGenerator
{
    Task<NpiNumber> NextAsync(CancellationToken cancellationToken);
}
