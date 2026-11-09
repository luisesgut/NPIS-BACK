using System.Text.Json;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Exceptions;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Domain.Entities;

/// <summary>Un campo de la captura que cambió al atender una solicitud de cambios (clave camelCase como en el frontend).</summary>
public sealed record FieldChange(string Field, string Before, string After);

/// <summary>Un cambio puntual que pidió el cliente sobre el PrintCard; Diseño lo palomea cuando ya está aplicado.</summary>
public sealed record ChecklistItem(Guid Id, string Text, DateTimeOffset? DoneAt = null, Guid? DoneByUserId = null, string? DoneByName = null, string? Comment = null)
{
    public const int CommentMaxLength = 1000;

    public bool IsDone => DoneAt is not null;

    /// <summary>Comentario recortado (null si viene vacío); falla si es demasiado largo.</summary>
    public static string? CleanComment(string? comment)
    {
        var text = comment?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > CommentMaxLength)
            throw new NpiValidationException("checklist-comment-too-long", $"El comentario no puede exceder {CommentMaxLength} caracteres.");
        return text;
    }
}

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
    public AreaType FromArea { get; private set; }
    public AreaType ToArea { get; }
    public ChangeRequestOrigin Origin { get; }
    public string Description { get; private set; }
    public IReadOnlyList<string> FieldKeys { get; private set; }

    /// <summary>Captura tal como estaba al pedir los cambios (JSON). null en solicitudes anteriores a esta función.</summary>
    public string? CaptureSnapshotJson { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public string? ResolvedByName { get; private set; }
    public string? ResponseComment { get; private set; }
    public string? ChangesAppliedJson { get; private set; }

    /// <summary>Archivo o link de apoyo (p. ej. captura de por qué se rechazaron las artes). Opcional.</summary>
    public Guid? EvidenceAttachmentId { get; private set; }

    /// <summary>Capturas de pantalla o fotos adjuntas al pedirla (ids de Attachments del NPI, separados por coma).</summary>
    public string? ImageAttachmentIdsCsv { get; private set; }
    public IReadOnlyList<Guid> ImageAttachmentIds => RequestImages.Parse(ImageAttachmentIdsCsv);
    internal void SetImages(IReadOnlyList<Guid> ids) => ImageAttachmentIdsCsv = RequestImages.Join(ids);

    /// <summary>Sólo en cambios pedidos por el cliente: la lista de cambios que Diseño va palomeando (JSON).</summary>
    public string? ChecklistJson { get; private set; }

    public bool IsResolved => ResolvedAt is not null;

    /// <summary>Quien la pidió la retiró antes de que Comercial la atendiera (cuenta como cerrada, sin cambios).</summary>
    public DateTimeOffset? WithdrawnAt { get; private set; }

    /// <summary>Quien la pidió agrega o quita campos, cambia comentarios o el motivo, antes de que Comercial la atienda.</summary>
    internal void Edit(string description, IReadOnlyList<string> fieldKeys)
    {
        Description = description;
        FieldKeys = fieldKeys;
    }

    internal void Withdraw(Actor actor, DateTimeOffset at)
    {
        WithdrawnAt = at;
        ResolvedAt = at;
        ResolvedByUserId = actor.UserId;
        ResolvedByName = actor.Name;
        ResponseComment = "Retirada por quien la pidió";
    }

    public IReadOnlyList<FieldChange> ChangesApplied =>
        string.IsNullOrEmpty(ChangesAppliedJson) ? [] : JsonSerializer.Deserialize<List<FieldChange>>(ChangesAppliedJson, Json) ?? [];

    /// <summary>
    /// Lo que hay que corregir en cada campo marcado (clave del campo → comentario), 2026-11-03. Comercial
    /// lo ve junto al campo en el formulario. Opcional: las solicitudes viejas sólo traen la descripción general.
    /// </summary>
    public string? FieldNotesJson { get; private set; }

    public IReadOnlyDictionary<string, string> FieldNotes =>
        string.IsNullOrEmpty(FieldNotesJson) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(FieldNotesJson, Json) ?? [];

    internal void SetFieldNotes(IReadOnlyDictionary<string, string> notes) =>
        FieldNotesJson = notes.Count == 0 ? null : JsonSerializer.Serialize(notes, Json);

    public IReadOnlyList<ChecklistItem> Checklist =>
        string.IsNullOrEmpty(ChecklistJson) ? [] : JsonSerializer.Deserialize<List<ChecklistItem>>(ChecklistJson, Json) ?? [];

    internal void SetChecklist(IEnumerable<ChecklistItem> items) => ChecklistJson = JsonSerializer.Serialize(items.ToList(), Json);

    /// <summary>Palomea (o despalomea) un cambio. Devuelve false si el renglón no es de esta solicitud.</summary>
    internal bool SetChecklistItemDone(Guid itemId, bool done, Actor actor, DateTimeOffset at, string? comment = null)
    {
        var items = Checklist.ToList();
        var index = items.FindIndex(i => i.Id == itemId);
        if (index < 0) return false;
        // El comentario de cada punto (2026-11-09): lo que respondió el cliente o lo que se hizo. Al
        // despalomear se conserva, para no perderlo si se vuelve a palomear.
        items[index] = done
            ? items[index] with { DoneAt = at, DoneByUserId = actor.UserId, DoneByName = actor.Name, Comment = ChecklistItem.CleanComment(comment) ?? items[index].Comment }
            : items[index] with { DoneAt = null, DoneByUserId = null, DoneByName = null };
        SetChecklist(items);
        return true;
    }

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

    /// <summary>AdminTotal corrige qué área la pidió (se registró desde la tarjeta equivocada).</summary>
    internal void CorrectFromArea(AreaType area) => FromArea = area;

    /// <summary>
    /// Reconfirmación de Ingeniería: suma campos cambiados a los que ya esperaban revisión. Por campo se
    /// queda el valor "antes" más viejo y el "después" más nuevo; si regresó a como estaba, sale de la lista.
    /// </summary>
    internal void MergePendingChanges(IEnumerable<FieldChange> changes)
    {
        var merged = ChangesApplied.ToList();
        foreach (var change in changes)
        {
            var index = merged.FindIndex(c => c.Field == change.Field);
            if (index < 0) merged.Add(change);
            else merged[index] = merged[index] with { After = change.After };
        }
        merged.RemoveAll(c => c.Before == c.After);
        ChangesAppliedJson = JsonSerializer.Serialize(merged, Json);
    }

    /// <summary>Reconfirmación de Ingeniería: la da por revisada sin recalcular los cambios (ya se calcularon al abrirla).</summary>
    internal void Acknowledge(Actor actor, DateTimeOffset at, string? comment)
    {
        ResolvedAt = at;
        ResolvedByUserId = actor.UserId;
        ResolvedByName = actor.Name;
        ResponseComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

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
