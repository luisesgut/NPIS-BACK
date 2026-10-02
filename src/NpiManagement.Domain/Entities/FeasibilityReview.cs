namespace NpiManagement.Domain.Entities;

/// <summary>
/// Aprobación de factibilidad de un área (Ingeniería o Diseño). Máximo una por área por NPI.
/// Las dos son independientes y paralelas — ninguna requiere que la otra exista (decisión 1
/// del propietario, ver docs/architecture-decisions.md ADR-006).
/// </summary>
public sealed class FeasibilityReview
{
    public Guid Id { get; }
    public AreaType Area { get; }
    public Guid ApprovedByUserId { get; }
    public string ApprovedByName { get; }
    public DateTimeOffset ApprovedAt { get; }
    public string? Note { get; }
    /// <summary>
    /// Claves de campo (catalogadas en npiFields.ts) que el revisor dejó explícitamente marcadas
    /// como "revisadas y correctas" al aprobar — solo se usa para Ingeniería (Diseño no tiene esta
    /// revisión campo por campo). Es el rastro de auditoría de qué se revisó, no solo que se aprobó.
    /// </summary>
    public IReadOnlyList<string> ConfirmedFieldKeys { get; }

    internal FeasibilityReview(Guid id, AreaType area, Guid approvedByUserId, string approvedByName, DateTimeOffset approvedAt, string? note, IReadOnlyList<string>? confirmedFieldKeys = null)
    {
        if (area is not (AreaType.Ingenieria or AreaType.Diseno))
            throw new ArgumentOutOfRangeException(nameof(area), "La factibilidad sólo aplica a Ingeniería o Diseño.");

        Id = id;
        Area = area;
        ApprovedByUserId = approvedByUserId;
        ApprovedByName = approvedByName;
        ApprovedAt = approvedAt;
        Note = note;
        ConfirmedFieldKeys = confirmedFieldKeys ?? [];
    }
}
