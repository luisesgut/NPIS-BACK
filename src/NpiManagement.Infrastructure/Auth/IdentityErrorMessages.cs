using Microsoft.AspNetCore.Identity;

namespace NpiManagement.Infrastructure.Auth;

/// <summary>
/// Los mensajes por defecto de Identity vienen en inglés, y éstos los lee directamente quien se
/// registra o cambia su contraseña. Se traducen los que puede provocar al escribirla; cualquier otro
/// pasa tal cual antes que ocultarlo.
/// </summary>
public static class IdentityErrorMessages
{
    private static readonly Dictionary<string, string> Spanish = new()
    {
        [nameof(IdentityErrorDescriber.PasswordMismatch)] = "La contraseña actual no es correcta.",
        // El 8 es Password.RequiredLength en DependencyInjection.cs.
        [nameof(IdentityErrorDescriber.PasswordTooShort)] = "La contraseña debe tener al menos 8 caracteres.",
        [nameof(IdentityErrorDescriber.PasswordRequiresDigit)] = "La contraseña debe incluir al menos un número.",
        [nameof(IdentityErrorDescriber.PasswordRequiresLower)] = "La contraseña debe incluir al menos una minúscula.",
        [nameof(IdentityErrorDescriber.PasswordRequiresUpper)] = "La contraseña debe incluir al menos una mayúscula.",
        [nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric)] = "La contraseña debe incluir al menos un símbolo, por ejemplo ! @ # $.",
        [nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars)] = "La contraseña debe tener más caracteres distintos.",
    };

    public static string[] Translate(IEnumerable<IdentityError> errors) =>
        errors.Select(e => Spanish.GetValueOrDefault(e.Code, e.Description)).ToArray();
}
