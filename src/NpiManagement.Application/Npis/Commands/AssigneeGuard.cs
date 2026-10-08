using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Una asignación sólo puede recaer en un usuario activo del área correspondiente. Antes no se
/// validaba: el selector de preasignación de un AdminComercial listaba vendedores (ver
/// UsersController.List) y el NPI quedaba con un vendedor como "ingeniero" que nadie de
/// Ingeniería veía.
/// </summary>
internal static class AssigneeGuard
{
    public static async Task EnsureAssignableAsync(IUserDirectory userDirectory, Guid userId, AreaType area, CancellationToken cancellationToken)
    {
        var user = await userDirectory.FindAsync(userId, cancellationToken);
        if (user is null || user.Area != area || user.Status != UserStatus.Active)
            throw new RequestValidationException("assignee-not-valid", $"La persona seleccionada no es un usuario activo de {AreaLabel(area)}.");
    }

    private static string AreaLabel(AreaType area) => area switch
    {
        AreaType.Ingenieria => "Ingeniería",
        AreaType.Diseno => "Diseño",
        AreaType.Comercial => "Comercial",
        _ => area.ToString(),
    };
}
