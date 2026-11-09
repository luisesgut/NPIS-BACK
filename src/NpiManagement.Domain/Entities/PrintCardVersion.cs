using System.Text.Json;
using NpiManagement.Domain.Common;

namespace NpiManagement.Domain.Entities;

/// <summary>
/// Una versión del documento PrintCard. Orden de revisión: Diseño la genera (queda aprobada por
/// Diseño en ese momento), Ingeniería la aprueba o la rechaza, y sólo con la aprobación de
/// Ingeniería Comercial la envía al cliente. Cada versión nueva abre su propia ronda; las de
/// versiones anteriores quedan históricas, ligadas a su versión (ver docs/domain-model.md invariante 4).
/// </summary>
public sealed class PrintCardVersion
{
    public Guid Id { get; }
    public int VersionNumber { get; }
    public string Code { get; }
    public Guid? AttachmentId { get; private set; }
    public Guid UploadedByUserId { get; }
    public string UploadedByName { get; }
    public DateTimeOffset UploadedAt { get; }
    public string? Note { get; }

    /// <summary>Ingeniería rechazó esta versión: Diseño debe generar otra.</summary>
    public DateTimeOffset? RejectedAt { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public string? RejectedByName { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? RejectionEvidenceAttachmentId { get; private set; }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Puntos que Diseño le deja a Comercial para revisar con el cliente en esta versión (2026-11-03).
    /// Comercial los palomea; la aprobación del cliente no se registra mientras falte alguno.
    /// </summary>
    public string? CommercialChecklistJson { get; private set; }

    public IReadOnlyList<ChecklistItem> CommercialChecklist =>
        string.IsNullOrEmpty(CommercialChecklistJson) ? [] : JsonSerializer.Deserialize<List<ChecklistItem>>(CommercialChecklistJson, Json) ?? [];

    internal void SetCommercialChecklist(IEnumerable<ChecklistItem> items) => CommercialChecklistJson = JsonSerializer.Serialize(items.ToList(), Json);

    /// <summary>Palomea (o despalomea) un punto. Devuelve false si el renglón no es de esta versión.</summary>
    internal bool SetCommercialChecklistItemDone(Guid itemId, bool done, Actor actor, DateTimeOffset at, string? comment = null)
    {
        var items = CommercialChecklist.ToList();
        var index = items.FindIndex(i => i.Id == itemId);
        if (index < 0) return false;
        // El comentario de cada punto (2026-11-09): lo que respondió el cliente o lo que se hizo. Al
        // despalomear se conserva, para no perderlo si se vuelve a palomear.
        items[index] = done
            ? items[index] with { DoneAt = at, DoneByUserId = actor.UserId, DoneByName = actor.Name, Comment = ChecklistItem.CleanComment(comment) ?? items[index].Comment }
            : items[index] with { DoneAt = null, DoneByUserId = null, DoneByName = null };
        SetCommercialChecklist(items);
        return true;
    }

    private readonly List<PrintCardApproval> _approvals = [];
    public IReadOnlyCollection<PrintCardApproval> Approvals => _approvals;

    internal PrintCardVersion(
        Guid id,
        int versionNumber,
        string code,
        Guid? attachmentId,
        Guid uploadedByUserId,
        string uploadedByName,
        DateTimeOffset uploadedAt,
        string? note)
    {
        Id = id;
        VersionNumber = versionNumber;
        Code = code;
        AttachmentId = attachmentId;
        UploadedByUserId = uploadedByUserId;
        UploadedByName = uploadedByName;
        UploadedAt = uploadedAt;
        Note = note;
    }

    public bool IsRejected => RejectedAt is not null;

    /// <summary>
    /// Lista para cargarse en sistema: Ingeniería y Cliente aprobaron. La de Diseño se registra sola
    /// al generar la versión, así que no se exige (las versiones anteriores a esa regla no la tienen).
    /// </summary>
    public bool IsFullyApproved =>
        !IsRejected &&
        _approvals.Any(a => a.Role == ApproverRole.Engineering) &&
        _approvals.Any(a => a.Role == ApproverRole.Client);

    internal bool HasApprovalFrom(ApproverRole role) => _approvals.Any(a => a.Role == role);

    internal PrintCardApproval AddApproval(Guid id, ApproverRole role, Guid byUserId, string byName, DateTimeOffset at, string? note, Guid? evidenceAttachmentId)
    {
        var approval = new PrintCardApproval(id, Id, role, byUserId, byName, at, note, evidenceAttachmentId);
        _approvals.Add(approval);
        return approval;
    }

    /// <summary>Deshacer: quita la aprobación de un rol (la que se dio por error).</summary>
    internal void RemoveApproval(ApproverRole role) => _approvals.RemoveAll(a => a.Role == role);

    /// <summary>Deshacer: quita el rechazo de Ingeniería (la versión vuelve a estar en su revisión).</summary>
    /// <summary>Diseño subió el archivo equivocado y lo cambia antes de que Ingeniería revise (misma versión).</summary>
    internal void ReplaceFile(Guid attachmentId) => AttachmentId = attachmentId;

    internal void ClearRejection()
    {
        RejectedAt = null;
        RejectedByUserId = null;
        RejectedByName = null;
        RejectionReason = null;
        RejectionEvidenceAttachmentId = null;
    }

    internal void Reject(Guid byUserId, string byName, DateTimeOffset at, string reason, Guid? evidenceAttachmentId)
    {
        RejectedAt = at;
        RejectedByUserId = byUserId;
        RejectedByName = byName;
        RejectionReason = reason;
        RejectionEvidenceAttachmentId = evidenceAttachmentId;
    }
}
