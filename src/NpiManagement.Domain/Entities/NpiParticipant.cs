namespace NpiManagement.Domain.Entities;

/// <summary>
/// Persona que trabaja el NPI junto con el responsable de su área ("equipo por NPI", 2026-11-10).
/// Puede hacer todo lo que hace el responsable de esa área, pero el responsable sigue siendo uno
/// solo: es a quien le corre el semáforo, a quien le llegan los correos y quien cuenta en las métricas.
/// </summary>
public sealed class NpiParticipant
{
    public Guid Id { get; }
    public Guid UserId { get; }
    /// <summary>Área en la que participa (Comercial, Ingeniería o Diseño).</summary>
    public AreaType Area { get; }
    public Guid AddedByUserId { get; }
    public string AddedByName { get; }
    public DateTimeOffset AddedAt { get; }

    // Los nombres de los parámetros coinciden con las propiedades: EF Core materializa con este constructor.
    internal NpiParticipant(Guid id, Guid userId, AreaType area, Guid addedByUserId, string addedByName, DateTimeOffset addedAt)
    {
        Id = id;
        UserId = userId;
        Area = area;
        AddedByUserId = addedByUserId;
        AddedByName = addedByName;
        AddedAt = addedAt;
    }
}
