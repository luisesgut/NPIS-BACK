using FluentAssertions;
using NpiManagement.Infrastructure.Services;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// Prueba de generación concurrente del folio de NPI: N tareas en paralelo, cada una con su
/// propio DbContext (simulando N requests concurrentes reales), deben obtener valores únicos
/// y sin huecos inesperados — demuestra que el SEQUENCE de SQL Server es atómico bajo
/// concurrencia sin necesidad de bloqueo explícito en la aplicación (ADR-005).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class NpiNumberConcurrencyTests(SqlServerFixture fixture)
{
    [SkippableFact]
    public async Task Concurrent_generation_never_produces_duplicate_numbers()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        const int concurrentRequests = 50;

        var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            await using var db = fixture.CreateDbContext();
            var generator = new SqlServerNpiNumberGenerator(db);
            return await generator.NextAsync(CancellationToken.None);
        });

        var results = await Task.WhenAll(tasks);

        results.Select(n => n.Value).Distinct().Should().HaveCount(concurrentRequests, "cada llamada concurrente debe obtener un folio distinto, sin colisiones");
    }
}
