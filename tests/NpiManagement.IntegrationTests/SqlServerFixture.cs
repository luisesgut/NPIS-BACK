using Microsoft.EntityFrameworkCore;
using NpiManagement.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// Levanta un contenedor real de SQL Server (Testcontainers) para las pruebas de integración.
/// Si Docker no está disponible en el entorno, NO falla — marca <see cref="IsAvailable"/> en
/// false y las pruebas que dependen de esta fixture se omiten explícitamente (SkippableFact),
/// nunca se sustituyen por EF InMemory (instrucción explícita: InMemory no reproduce
/// restricciones/relaciones reales de SQL Server).
///
/// Cómo ejecutar estas pruebas con Docker disponible:
///   1. Instala/arranca Docker Desktop (o cualquier motor compatible con Testcontainers).
///   2. `dotnet test tests/NpiManagement.IntegrationTests`
/// La imagen se descarga automáticamente (mcr.microsoft.com/mssql/server:2022-latest) y el
/// contenedor se destruye al finalizar la corrida.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public bool IsAvailable { get; private set; }
    public string SkipReason { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();

            await _container.StartAsync();

            await using var dbContext = CreateDbContext();
            await dbContext.Database.MigrateAsync();

            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            SkipReason = $"SQL Server (Testcontainers) no disponible en este entorno — probablemente Docker no está " +
                         $"instalado o accesible. Detalle: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("El contenedor de SQL Server no se inicializó.");

    /// <summary>Nueva instancia de DbContext contra el contenedor — cada prueba/consulta usa la suya.</summary>
    public NpiManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NpiManagementDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new NpiManagementDbContext(options);
    }
}
