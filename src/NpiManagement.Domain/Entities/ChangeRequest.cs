using System.Text.Json;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Domain.Entities;

/// <summary>Un campo de la captura que cambió al atender una solicitud de cambios (clave camelCase como en el frontend).</summary>
public sealed record FieldChange(string Field, string Before, string After);

/// <summary>
/// Solicitud de cambios sobre un NPI. Orígenes posibles: Ingeniería o Diseño→Comercial (rechazo con
/// cambios), Diseño→Comercial por artes rechazadas (ChangeRequestOrigin.ArtworkRejected), Comercial→Diseño
/// (cambios reportados por el cliente, ChangeRequestOrigin.ClientReported),
/// o cualquier otra interna. No es terminal — forma parte del ciclo normal del flujo.
///
/// Las de Ingeniería/Diseño→Comercial con cambios de captura guardan una foto de la captura al pedir los cambios; al reenviar,
/// Comercial la "resuelve" y queda registrado qué campos cambió (antes → después) y su comentario,
/// para que Ingeniería vea exactamente qué se corrigió.
/// </summary>
public sealed class ChangeRequest
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Guid Id { get; }
    public Guid RequestedByUserId { get; }
    public string RequestedByName { get; }
    public DateTimeOffset RequestedAt { get; }
    public AreaType FromArea { get; }
    public AreaType ToArea { get; }
    public ChangeRequestOrigin Origin { get; }
    public string Description { get; }
    public IReadOnlyList<string> FieldKeys { get; }

    /// <summary>Captura tal como estaba al pedir los cambios (JSON). null en solicitudes anteriores a esta función.</summary>
    public string? CaptureSnapshotJson { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public string? ResolvedByName { get; private set; }
    public string? ResponseComment { get; private set; }
    public string? ChangesAppliedJson { get; private set; }

    /// <summary>Archivo o link de apoyo (p. ej. captura de por qué se rechazaron las artes). Opcional.</summary>
    public Guid? EvidenceAttachmentId { get; private set; }

    public bool IsResolved => ResolvedAt is not null;

    public IReadOnlyList<FieldChange> ChangesApplied =>
        string.IsNullOrEmpty(ChangesAppliedJson) ? [] : JsonSerializer.Deserialize<List<FieldChange>>(ChangesAppliedJson, Json) ?? [];

    internal ChangeRequest(
        Guid id,
        Guid requestedByUserId,
        string requestedByName,
        DateTimeOffset requestedAt,
        AreaType fromArea,
        AreaType toArea,
        ChangeRequestOrigin origin,
        string description,
        IReadOnlyList<string> fieldKeys,
        string? captureSnapshotJson = null,
        Guid? evidenceAttachmentId = null)
    {
        Id = id;
        RequestedByUserId = requestedByUserId;
        RequestedByName = requestedByName;
        RequestedAt = requestedAt;
        FromArea = fromArea;
        ToArea = toArea;
        Origin = origin;
        Description = description;
        FieldKeys = fieldKeys;
        CaptureSnapshotJson = captureSnapshotJson;
        EvidenceAttachmentId = evidenceAttachmentId;
    }

    internal static string SnapshotOf(NpiCaptureData capture) => CaptureDiff.Snapshot(capture);

    /// <summary>Marca la solicitud como atendida y registra qué cambió respecto a la foto tomada al pedirla.</summary>
    internal void Resolve(Actor actor, DateTimeOffset at, string? comment, NpiCaptureData currentCapture)
    {
        var changes = CaptureDiff.FromSnapshot(CaptureSnapshotJson) is { } before
            ? CaptureDiff.Between(before, currentCapture)
            : [];

        ResolvedAt = at;
        ResolvedByUserId = actor.UserId;
        ResolvedByName = actor.Name;
        ResponseComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        ChangesAppliedJson = JsonSerializer.Serialize(changes, Json);
    }
}
