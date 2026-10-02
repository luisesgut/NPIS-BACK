namespace NpiManagement.Application.Exceptions;

/// <summary>
/// Recurso inexistente O fuera del alcance de autorización del usuario actual (mismo código de
/// error deliberadamente — ver docs/authorization-matrix.md §4.6: no se revela la existencia de
/// un recurso a quien no debería poder verlo). Api la traduce a 404.
/// </summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' no existe o no está disponible para el usuario actual.");
