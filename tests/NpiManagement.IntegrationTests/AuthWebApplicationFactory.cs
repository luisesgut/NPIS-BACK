using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// Levanta la API completa (Program.cs real, incluida la migración+seed automática de
/// Development) apuntando al contenedor de SQL Server de <see cref="SqlServerFixture"/>.
/// </summary>
public sealed class AuthWebApplicationFactory(string connectionString, string seedPassword) : WebApplicationFactory<Program>
{
    public const string SeedPassword = "Test-Passw0rd!2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:Issuer"] = "NpiManagement.IntegrationTests",
                ["Jwt:Audience"] = "NpiManagement.IntegrationTests.Clients",
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-characters",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Seed:UsersPassword"] = seedPassword,
                // Cada prueba levanta un contenedor SQL Server efímero y vacío (Testcontainers) —
                // a diferencia de Development/Production, aquí SÍ debe migrar automáticamente al
                // arrancar, porque no hay paso manual previo posible contra un contenedor que se
                // crea y se destruye por corrida (ver Program.cs y docs/demo-setup.md).
                ["Database:ApplyMigrationsOnStartup"] = "true",
            });
        });
    }
}
