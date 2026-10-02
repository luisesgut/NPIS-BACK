namespace NpiManagement.Domain.Common;

/// <summary>
/// Identidad del usuario que ejecuta una acción de negocio, tal como la resuelve Application
/// desde el token (nunca desde el body de la petición — ver docs/api-contract.md §12).
/// Se pasa como un solo parámetro a los métodos de Npi para no repetir (Guid, string, string)
/// en cada firma.
/// </summary>
public readonly record struct Actor(Guid UserId, string Role, string Name);
