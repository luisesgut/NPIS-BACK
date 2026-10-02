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
        NpiStageMilestones.FromNpi(npi).CompletedStages);

    public static NpiDetailDto ToDetail(Npi npi, IClock clock, IBusinessCalendar calendar, byte[] rowVersion) => new(
        npi.Id, rowVersion, npi.NpiNumber?.Value, npi.Priority.ToString(), NpiCaptureDto.FromDomain(npi.Capture),
        npi.LifecycleStatus.ToString(), npi.TerminalReason?.ToString(),
        npi.EngineeringAreaStatus.ToString(), npi.DesignAreaStatus.ToString(),
        npi.CreatedByUserId, npi.CreatedAt, npi.RequestDate,
        npi.AssignedEngineeringUserId, npi.AssignedDesignUserId, npi.AssignedCommercialUserId,
        ToTimerDto(npi.GlobalTimer, clock, calendar), ToTimerDto(npi.CommercialTimer, clock, calendar),
        ToTimerDto(npi.EngineeringTimer, clock, calendar), ToTimerDto(npi.DesignTimer, clock, calendar),
        npi.ArtesValidadasAt, npi.PrintCardSentToClientAt, npi.PrintCardLoadedAt, npi.CodigoPT, npi.PtName, npi.PtRegisteredAt,
        npi.PedidoSapNumber, npi.PedidoSapRegisteredAt,
        npi.EtCreatedAt, npi.EtLoadedInSisproAt,
        npi.Comments.Select(c => new CommentDto(c.Id, c.AuthorUserId, c.AuthorName, c.Text, c.CreatedAt)).ToList(),
        npi.Attachments.Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.Context.ToString(), a.Version, a.UploadedByUserId, a.UploadedAt)).ToList(),
        npi.ChangeRequests.Select(cr => new ChangeRequestDto(cr.Id, cr.RequestedByUserId, cr.RequestedByName, cr.RequestedAt, cr.FromArea.ToString(), cr.ToArea.ToString(), cr.Origin.ToString(), cr.Description, cr.FieldKeys)).ToList(),
        npi.InformationRequests.Select(r => new InformationRequestDto(r.Id, r.Type.ToString(), r.Status.ToString(), r.Text, r.RequestedByUserId, r.RequestedByName, r.RequestedAt, r.ResolvedAt, r.ResolvedByUserId, r.ResponseText)).ToList(),
        npi.FeasibilityReviews.Select(f => new FeasibilityReviewDto(f.Id, f.Area.ToString(), f.ApprovedByUserId, f.ApprovedByName, f.ApprovedAt, f.Note, f.ConfirmedFieldKeys)).ToList(),
        npi.PrintCardVersions.OrderBy(v => v.VersionNumber).Select(v => new PrintCardVersionDto(
            v.Id, v.VersionNumber, v.Code, v.AttachmentId, v.UploadedByUserId, v.UploadedByName, v.UploadedAt, v.Note,
            v.Approvals.Select(a => new PrintCardApprovalDto(a.Id, a.Role.ToString(), a.ByUserId, a.ByName, a.At, a.Note, a.EvidenceAttachmentId)).ToList()))
            .ToList());

    public static TimerEventDto ToTimerEventDto(Domain.Entities.TimerEvent e) =>
        new(e.Id, e.Stage.ToString(), e.Action.ToString(), e.OccurredAt, e.ActorUserId, e.DurationMsAtEvent, e.FromStage?.ToString(), e.ToStage?.ToString(), e.Note);

    public static AuditEventDto ToAuditEventDto(Domain.Entities.AuditEvent e) =>
        new(e.Id, e.ActorUserId, e.ActorRole, e.OccurredAt, e.Action, e.Details);

    public static TimerDto ToTimerDto(TimerState timer, IClock clock, IBusinessCalendar calendar) =>
        new(timer.HasStarted, timer.IsRunning, timer.IsClosed, timer.ElapsedMs(clock, calendar));
}
