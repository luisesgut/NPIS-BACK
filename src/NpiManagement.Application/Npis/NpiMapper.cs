using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Application.Npis;

public static class NpiMapper
{
    public static NpiListItemDto ToListItem(Npi npi, IClock clock, IBusinessCalendar calendar) => new(
        npi.Id, npi.NpiNumber?.Value, npi.Priority.ToString(), npi.LifecycleStatus.ToString(),
        npi.EngineeringAreaStatus.ToString(), npi.DesignAreaStatus.ToString(),
        npi.Capture.Cliente, npi.Capture.NumeroCotizacion, npi.Capture.DescripcionProducto,
        npi.RequestDate, npi.CreatedByUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId, npi.AssignedCommercialUserId,
        ToTimerDto(npi.GlobalTimer, clock, calendar), ToTimerDto(npi.CommercialTimer, clock, calendar),
        ToTimerDto(npi.EngineeringTimer, clock, calendar), ToTimerDto(npi.DesignTimer, clock, calendar),
        NpiStageMilestones.FromNpi(npi).CompletedStages,
        npi.PendingClosureProposal?.Area.ToString());

    public static NpiDetailDto ToDetail(Npi npi, IClock clock, IBusinessCalendar calendar, byte[] rowVersion) => new(
        npi.Id, rowVersion, npi.NpiNumber?.Value, npi.Priority.ToString(), NpiCaptureDto.FromDomain(npi.Capture),
        npi.LifecycleStatus.ToString(), npi.TerminalReason?.ToString(),
        npi.EngineeringAreaStatus.ToString(), npi.DesignAreaStatus.ToString(),
        npi.CreatedByUserId, npi.CreatedAt, npi.RequestDate,
        npi.AssignedEngineeringUserId, npi.AssignedDesignUserId, npi.AssignedCommercialUserId,
        ToTimerDto(npi.GlobalTimer, clock, calendar), ToTimerDto(npi.CommercialTimer, clock, calendar),
        ToTimerDto(npi.EngineeringTimer, clock, calendar), ToTimerDto(npi.DesignTimer, clock, calendar),
        npi.ArtesValidadasAt, npi.ArtClassification?.ToString(), npi.HasLabValues, npi.PrintCardSentToClientAt, npi.PrintCardLoadedAt, npi.CodigoPT, npi.PtName, npi.PtRegisteredAt,
        npi.PedidoSapNumber, npi.PedidoSapRegisteredAt,
        npi.EtCreatedAt, npi.EtLoadedInSisproAt,
        npi.Attachments.Select(ToAttachmentDto).ToList(),
        npi.ChangeRequests.Select(cr => new ChangeRequestDto(cr.Id, cr.RequestedByUserId, cr.RequestedByName, cr.RequestedAt, cr.FromArea.ToString(), cr.ToArea.ToString(), cr.Origin.ToString(), cr.Description, cr.FieldKeys,
            cr.ResolvedAt, cr.ResolvedByName, cr.ResponseComment, cr.ChangesApplied.Select(c => new FieldChangeDto(c.Field, c.Before, c.After)).ToList(), cr.EvidenceAttachmentId,
            cr.Checklist.Select(i => new ChecklistItemDto(i.Id, i.Text, i.DoneAt, i.DoneByName, i.Comment)).ToList(), cr.FieldNotes, cr.ImageAttachmentIds, cr.WithdrawnAt)).ToList(),
        npi.InformationRequests.Select(r => new InformationRequestDto(r.Id, r.Type.ToString(), r.Status.ToString(), r.Text, r.RequestedByUserId, r.RequestedByName, r.RequestedAt, r.ResolvedAt, r.ResolvedByUserId, r.ResponseText, r.ImageAttachmentIds)).ToList(),
        npi.FeasibilityReviews.Select(f => new FeasibilityReviewDto(f.Id, f.Area.ToString(), f.ApprovedByUserId, f.ApprovedByName, f.ApprovedAt, f.Note, f.ConfirmedFieldKeys)).ToList(),
        npi.PrintCardVersions.OrderBy(v => v.VersionNumber).Select(v => new PrintCardVersionDto(
            v.Id, v.VersionNumber, v.Code, v.AttachmentId, v.UploadedByUserId, v.UploadedByName, v.UploadedAt, v.Note,
            v.Approvals.Select(a => new PrintCardApprovalDto(a.Id, a.Role.ToString(), a.ByUserId, a.ByName, a.At, a.Note, a.EvidenceAttachmentId)).ToList(),
            v.RejectedAt, v.RejectedByName, v.RejectionReason, v.RejectionEvidenceAttachmentId,
            v.CommercialChecklist.Select(i => new ChecklistItemDto(i.Id, i.Text, i.DoneAt, i.DoneByName, i.Comment)).ToList()))
            .ToList(),
        npi.SisproQuotes.OrderBy(q => q.VersionNumber).Select(ToSisproQuoteDto).ToList(),
        npi.ClosureProposals.OrderBy(p => p.ProposedAt).Select(p => new ClosureProposalDto(
            p.Id, p.Reason.ToString(), p.Area.ToString(), p.Status.ToString(),
            p.ProposedByUserId, p.ProposedByName, p.ProposedAt, p.Note, p.EvidenceAttachmentId,
            p.DecidedByName, p.DecidedAt, p.DecisionNote)).ToList(),
        npi.UndoableLastAction is { } undo ? new UndoableActionDto(undo.Action, undo.Details, undo.ActorUserId, undo.OccurredAt) : null,
        npi.ClosingEvent is { } closing ? new ClosingInfoDto(closing.Details, closing.ActorRole, closing.OccurredAt) : null,
        npi.DesignInstructions,
        npi.PtSapTable,
        npi.FinalPrintCardVersion?.Id,
        Participants: npi.Participants.OrderBy(p => p.AddedAt)
            .Select(p => new NpiParticipantDto(p.UserId, p.Area.ToString(), p.AddedByName, p.AddedAt)).ToList());

    private static readonly System.Text.Json.JsonSerializerOptions SnapshotJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>La foto que se guarda al ligar una cotización (mismo formato que lee ToSisproQuoteDto).</summary>
    public static string SerializeSnapshot(Abstractions.QuotationSnapshot snapshot) =>
        System.Text.Json.JsonSerializer.Serialize(snapshot, SnapshotJson);

    public static SisproQuoteDto ToSisproQuoteDto(Domain.Entities.SisproQuoteLink q) => new(
        q.Id, q.VersionNumber, q.NumeroCotizacion, q.EstadoSispro, q.Status.ToString(),
        q.LinkedByUserId, q.LinkedByName, q.LinkedAt, q.StructureWarnings,
        q.RespondedAt, q.RespondedByName, q.ResponseNote, q.EvidenceAttachmentId,
        q.InvalidatedAt, q.InvalidationReason,
        ReadSnapshot(q.SnapshotJson));

    // Si algún día cambia el formato de la foto, el detalle no debe romperse: se muestra sin ella.
    private static Abstractions.QuotationSnapshot? ReadSnapshot(string json)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<Abstractions.QuotationSnapshot>(json, SnapshotJson); }
        catch (System.Text.Json.JsonException) { return null; }
        catch (NotSupportedException) { return null; }
    }

    public static AttachmentDto ToAttachmentDto(Domain.Entities.Attachment a) =>
        new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.Context.ToString(), a.Version, a.UploadedByUserId, a.UploadedAt, a.Url);

    public static TimerEventDto ToTimerEventDto(Domain.Entities.TimerEvent e) =>
        new(e.Id, e.Stage.ToString(), e.Action.ToString(), e.OccurredAt, e.ActorUserId, e.DurationMsAtEvent, e.FromStage?.ToString(), e.ToStage?.ToString(), e.Note);

    public static AuditEventDto ToAuditEventDto(Domain.Entities.AuditEvent e) =>
        new(e.Id, e.ActorUserId, e.ActorRole, e.OccurredAt, e.Action, e.Details);

    public static TimerDto ToTimerDto(TimerState timer, IClock clock, IBusinessCalendar calendar)
    {
        var now = clock.UtcNow;
        if (!timer.IsRunning)
            return new(timer.HasStarted, false, timer.IsClosed, timer.ElapsedMs(clock, calendar));

        var msUntilChange = (long)(calendar.NextBoundary(now) - now).TotalMilliseconds;
        return new(timer.HasStarted, true, timer.IsClosed, timer.ElapsedMs(clock, calendar), calendar.IsBusinessTime(now), msUntilChange);
    }
}
