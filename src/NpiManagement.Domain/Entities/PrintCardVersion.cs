namespace NpiManagement.Domain.Entities;

/// <summary>
/// Una versión del documento PrintCard. Cada nueva versión abre una nueva ronda de las tres
/// aprobaciones (Diseño/Ingeniería/Cliente) — las aprobaciones de versiones anteriores quedan
/// históricas, ligadas a su propia versión (ver docs/domain-model.md invariante 4).
/// </summary>
public sealed class PrintCardVersion
{
    public Guid Id { get; }
    public int VersionNumber { get; }
    public string Code { get; }
    public Guid? AttachmentId { get; }
    public Guid UploadedByUserId { get; }
    public string UploadedByName { get; }
    public DateTimeOffset UploadedAt { get; }
    public string? Note { get; }

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

    public bool IsFullyApproved =>
        _approvals.Any(a => a.Role == ApproverRole.Design) &&
        _approvals.Any(a => a.Role == ApproverRole.Engineering) &&
        _approvals.Any(a => a.Role == ApproverRole.Client);

    internal bool HasApprovalFrom(ApproverRole role) => _approvals.Any(a => a.Role == role);

    internal PrintCardApproval AddApproval(Guid id, ApproverRole role, Guid byUserId, string byName, DateTimeOffset at, string? note, Guid? evidenceAttachmentId)
    {
        var approval = new PrintCardApproval(id, Id, role, byUserId, byName, at, note, evidenceAttachmentId);
        _approvals.Add(approval);
        return approval;
    }
}
