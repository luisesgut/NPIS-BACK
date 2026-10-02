namespace NpiManagement.Application.Exceptions;

/// <summary>
/// Violación de una regla de validación de la solicitud que no depende de un invariante del
/// agregado (p. ej. límites de adjuntos) — Api la traduce a 400, igual que FluentValidation.
/// </summary>
public sealed class RequestValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
