namespace NpiManagement.Application.Exceptions;

/// <summary>
/// Un sistema externo del que depende la acción (p. ej. SISPRO) no respondió, tardó demasiado o
/// rechazó las credenciales. Api la traduce a 503: el usuario puede reintentar o seguir capturando a
/// mano. El mensaje es para el usuario; el detalle técnico va al log, nunca a la respuesta.
/// </summary>
public sealed class ExternalServiceUnavailableException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
}
