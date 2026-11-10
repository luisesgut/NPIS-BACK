using System.Reflection;
using FluentAssertions;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// ASP.NET rechaza con 500 (al validar) un record de petición cuya validación está en la propiedad
/// (<c>[property: Required]</c>) en vez del parámetro del constructor. Pasó con ChangeUserRoleRequest
/// en producción (2026-11-10); esta prueba revisa todos los contratos de la API sin levantar el servidor.
/// </summary>
public sealed class RequestContractsTests
{
    [Fact]
    public void No_request_record_puts_validation_on_its_properties()
    {
        var api = typeof(NpiManagement.Api.Controllers.UsersController).Assembly;
        var offenders = api.GetTypes()
            .Where(t => t.Namespace?.StartsWith("NpiManagement.Api.Contracts") == true && t.GetMethod("<Clone>$") is not null)
            .SelectMany(t =>
            {
                var ctorParams = t.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.Name).ToHashSet();
                return t.GetProperties()
                    .Where(p => ctorParams.Contains(p.Name)
                        && p.GetCustomAttributes<System.ComponentModel.DataAnnotations.ValidationAttribute>(inherit: true).Any())
                    .Select(p => $"{t.Name}.{p.Name}");
            })
            .ToList();

        offenders.Should().BeEmpty();
    }
}
