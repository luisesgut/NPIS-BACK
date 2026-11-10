using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Lectura de listado paginada — sólo trae columnas de resumen (nunca las colecciones satélite
/// completas de cada NPI) y filtra con WHERE en SQL, no en memoria (authorization-matrix.md §0).
/// Los 4 cronómetros sí se incluyen (Fase 8): son columnas propias del Npi, no colecciones.
/// </summary>
public sealed class EfNpiListReader(NpiManagementDbContext dbContext, IClock clock, IBusinessCalendar calendar) : INpiListReader
{
    public async Task<PagedResult<NpiListItemDto>> ListAsync(NpiListFilter filter, Expression<Func<Npi, bool>> accessPredicate, CancellationToken cancellationToken)
    {
        var query = dbContext.Npis.AsNoTracking().Where(accessPredicate);

        // "¿Qué necesita?" / "¿Dónde está?": el handler ya los resolvió a ids (salen de los turnos).
        if (filter.OnlyIds is { } onlyIds)
            query = query.Where(n => onlyIds.Contains(n.Id));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            // También por el nombre de quien lo tiene asignado (Comercial, Ingeniería o Diseño).
            var usersMatching = dbContext.Users.Where(u => EF.Functions.Like(u.FullName, $"%{term}%")).Select(u => u.Id);
            query = query.Where(n =>
                EF.Functions.Like(n.Capture.Cliente, $"%{term}%") ||
                EF.Functions.Like(n.Capture.NumeroCotizacion, $"%{term}%") ||
                EF.Functions.Like(n.Capture.DescripcionProducto, $"%{term}%") ||
                // Por código de PrintCard (cualquier versión) o código PT.
                n.PrintCardVersions.Any(v => EF.Functions.Like(v.Code, $"%{term}%")) ||
                EF.Functions.Like(n.CodigoPT, $"%{term}%") ||
                usersMatching.Any(id => id == n.AssignedCommercialUserId || id == n.AssignedEngineeringUserId || id == n.AssignedDesignUserId));
        }

        if (!string.IsNullOrWhiteSpace(filter.LifecycleStatus))
        {
            var statuses = filter.LifecycleStatus
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<LifecycleStatus>(s, true, out var status) ? status : (LifecycleStatus?)null)
                .OfType<LifecycleStatus>()
                .Distinct()
                .ToList();
            if (statuses.Count > 0)
                query = query.Where(n => statuses.Contains(n.LifecycleStatus));
        }

        if (!string.IsNullOrWhiteSpace(filter.Cliente))
        {
            var cliente = filter.Cliente.Trim();
            query = query.Where(n => n.Capture.Cliente == cliente);
        }

        if (!string.IsNullOrWhiteSpace(filter.Priority) && Enum.TryParse<Priority>(filter.Priority, true, out var priority))
            query = query.Where(n => n.Priority == priority);

        if (filter.CreatedByUserId is { } createdBy)
            query = query.Where(n => n.CreatedByUserId == createdBy);

        if (filter.AssignedToUserId is { } assignedTo)
            query = query.Where(n =>
                n.AssignedCommercialUserId == assignedTo ||
                n.AssignedEngineeringUserId == assignedTo ||
                n.AssignedDesignUserId == assignedTo ||
                n.Participants.Any(p => p.UserId == assignedTo));

        if (!string.IsNullOrWhiteSpace(filter.PendingClosureArea))
        {
            // Sólo NPIs activos: al cerrarse por otra vía, la propuesta queda sin efecto.
            query = query.Where(n => n.LifecycleStatus == LifecycleStatus.Active);
            query = Enum.TryParse<AreaType>(filter.PendingClosureArea, true, out var closureArea)
                ? query.Where(n => n.ClosureProposals.Any(p => p.Status == ClosureProposalStatus.Pending && p.Area == closureArea))
                : query.Where(n => n.ClosureProposals.Any(p => p.Status == ClosureProposalStatus.Pending));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = filter.NormalizedPage;
        var pageSize = filter.NormalizedPageSize;

        // Proyección intermedia: sólo columnas simples/valores propios, traducibles a SQL.
        // El .Value de NpiNumber y los .ToString() de enum se resuelven después, en memoria,
        // porque EF no puede traducir el acceso a miembros de un tipo con value converter.
        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.NpiNumber,
                n.Priority,
                n.LifecycleStatus,
                n.EngineeringAreaStatus,
                n.DesignAreaStatus,
                n.Capture.Cliente,
                n.Capture.NumeroCotizacion,
                n.Capture.DescripcionProducto,
                n.RequestDate,
                n.CreatedByUserId,
                n.AssignedEngineeringUserId,
                n.AssignedDesignUserId,
                n.AssignedCommercialUserId,
                n.GlobalTimer,
                n.CommercialTimer,
                n.EngineeringTimer,
                n.DesignTimer,
                // Hitos de la ruta: los que viven en colecciones se resuelven como EXISTS en la
                // misma consulta, sin cargar factibilidades ni versiones de PrintCard por fila.
                EngineeringFeasibilityApproved = n.FeasibilityReviews.Any(f => f.Area == AreaType.Ingenieria),
                DesignFeasibilityApproved = n.FeasibilityReviews.Any(f => f.Area == AreaType.Diseno),
                ClientApprovedPrintCard = n.PrintCardVersions.Any(v => v.Approvals.Any(a => a.Role == ApproverRole.Client)),
                PrintCardSentToClient = n.PrintCardSentToClientAt != null,
                PrintCardLoaded = n.PrintCardLoadedAt != null,
                PtRegistered = n.PtRegisteredAt != null,
                SapOrderRegistered = n.PedidoSapNumber != null,
                EtLoadedInSispro = n.EtLoadedInSisproAt != null,
                // Mismo criterio que NpiStageMilestones.CountsAsLinked / Npi.SisproQuoteAccepted: una
                // aceptada impide ligar otra, así que "existe una aceptada" equivale a "la vigente está aceptada".
                SisproQuoteLinked = n.SisproQuotes.Any(q => q.Status != SisproQuoteStatus.Superseded && q.Status != SisproQuoteStatus.Invalidated),
                SisproQuoteAccepted = n.SisproQuotes.Any(q => q.Status == SisproQuoteStatus.Accepted),
                HasPrintCard = n.PrintCardVersions.Any(),
                PendingClosureArea = n.ClosureProposals.Where(p => p.Status == ClosureProposalStatus.Pending).Select(p => (AreaType?)p.Area).FirstOrDefault(),
                n.ArtClassification,
                n.HasLabValues,
                PrintCardCode = n.PrintCardVersions.OrderByDescending(v => v.VersionNumber).Select(v => v.Code).FirstOrDefault(),
                LatestPrintCardNumber = n.PrintCardVersions.Max(v => (int?)v.VersionNumber),
                // La versión vigente (la de número más alto): ¿rechazada?, ¿la liberó Ingeniería?, ¿la aprobó el cliente?
                CurrentPrintCard = n.PrintCardVersions.OrderByDescending(v => v.VersionNumber).Select(v => new
                {
                    v.UploadedAt,
                    v.RejectionReason,
                    Rejected = v.RejectedAt != null,
                    EngineeringApproved = v.Approvals.Any(a => a.Role == ApproverRole.Engineering),
                    ClientApproved = v.Approvals.Any(a => a.Role == ApproverRole.Client),
                }).FirstOrDefault(),
                // La versión final: la que se cargó en sistema (Npi.FinalPrintCardVersion).
                LoadedPrintCardCode = n.PrintCardVersions.Where(v => v.Id == n.PrintCardLoadedVersionId).Select(v => v.Code).FirstOrDefault(),
                LoadedPrintCardNumber = n.PrintCardVersions.Where(v => v.Id == n.PrintCardLoadedVersionId).Select(v => (int?)v.VersionNumber).FirstOrDefault(),
                n.CodigoPT,
                // Para que la etapa diga quién pidió qué (2026-11-09).
                LatestOpenClientChangeAt = n.ChangeRequests.Where(c => c.Origin == ChangeRequestOrigin.ClientReported && c.ResolvedAt == null).Max(c => (DateTimeOffset?)c.RequestedAt),
                PendingCommercialInfo = n.InformationRequests.Any(i => i.Type == InformationRequestType.Commercial && i.Status == InformationRequestStatus.Pending),
                PendingEngineeringInfo = n.InformationRequests.Any(i => i.Type == InformationRequestType.Engineering && i.Status == InformationRequestStatus.Pending),
                PendingReconfirmation = n.ChangeRequests.Any(c => c.Origin == ChangeRequestOrigin.Reconfirmation && c.ResolvedAt == null),
                CommercialEditing = n.ChangeRequests.Any(c => c.Origin == ChangeRequestOrigin.CommercialEdit && c.ResolvedAt == null),
                Team = n.Participants.OrderBy(p => p.AddedAt).Select(p => new { p.UserId, p.Area }).ToList(),
            })
            .ToListAsync(cancellationToken);

        // Nombres de los responsables de la página, en una sola consulta.
        var userIds = rows
            .SelectMany(r => new[] { (Guid?)r.AssignedCommercialUserId, r.AssignedEngineeringUserId, r.AssignedDesignUserId }.Concat(r.Team.Select(t => (Guid?)t.UserId)))
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var names = await dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        string? NameOf(Guid? id) => id is { } value && names.TryGetValue(value, out var name) ? name : null;

        var items = rows.Select(r => new NpiListItemDto(
                r.Id, r.NpiNumber?.Value, r.Priority.ToString(), r.LifecycleStatus.ToString(),
                r.EngineeringAreaStatus.ToString(), r.DesignAreaStatus.ToString(),
                r.Cliente, r.NumeroCotizacion, r.DescripcionProducto, r.RequestDate,
                r.CreatedByUserId, r.AssignedEngineeringUserId, r.AssignedDesignUserId, r.AssignedCommercialUserId,
                NpiMapper.ToTimerDto(r.GlobalTimer, clock, calendar), NpiMapper.ToTimerDto(r.CommercialTimer, clock, calendar),
                NpiMapper.ToTimerDto(r.EngineeringTimer, clock, calendar), NpiMapper.ToTimerDto(r.DesignTimer, clock, calendar),
                new NpiStageMilestones(
                    r.EngineeringFeasibilityApproved, r.DesignFeasibilityApproved, r.PrintCardSentToClient, r.ClientApprovedPrintCard,
                    r.PrintCardLoaded, r.PtRegistered, r.SapOrderRegistered, r.EtLoadedInSispro,
                    r.SisproQuoteLinked, r.SisproQuoteAccepted, r.HasPrintCard).CompletedStages,
                // Un NPI ya cerrado no espera visto bueno (el dominio deja sin efecto la propuesta al cerrarse).
                IsTerminal(r.LifecycleStatus) ? null : r.PendingClosureArea?.ToString(),
                r.ArtClassification?.ToString(), r.HasLabValues,
                NameOf(r.AssignedCommercialUserId), NameOf(r.AssignedEngineeringUserId), NameOf(r.AssignedDesignUserId),
                r.PrintCardLoaded ? r.LoadedPrintCardCode ?? r.PrintCardCode : r.PrintCardCode, r.CodigoPT,
                r.PrintCardLoaded ? r.LoadedPrintCardNumber ?? r.LatestPrintCardNumber : null,
                PrintCardStepOf(r.CurrentPrintCard is null ? null : new PrintCardFacts(
                        r.CurrentPrintCard.Rejected, r.CurrentPrintCard.RejectionReason,
                        // Igual que Npi.PendingClientChangeRequest: los cambios anteriores a la versión vigente ya los atendió esa versión.
                        r.LatestOpenClientChangeAt is { } changeAt && changeAt >= r.CurrentPrintCard.UploadedAt,
                        r.CurrentPrintCard.EngineeringApproved, r.PrintCardSentToClient, r.CurrentPrintCard.ClientApproved),
                    r.PrintCardLoaded),
                (r.PendingCommercialInfo, r.PendingEngineeringInfo) switch
                {
                    (true, true) => "Ambas",
                    (true, false) => "Comercial",
                    (false, true) => "Ingenieria",
                    _ => null,
                },
                r.PendingReconfirmation, r.CommercialEditing,
                Team: r.Team.Select(t => new NpiTeamMemberDto(t.UserId, NameOf(t.UserId) ?? GetNpiAssigneesQueryHandler.UnknownUserName, t.Area.ToString())).ToList()))
            .ToList();

        return new PagedResult<NpiListItemDto>(items, page, pageSize, totalCount);
    }

    private sealed record PrintCardFacts(bool Rejected, string? RejectionReason, bool ClientChanges, bool EngineeringApproved, bool SentToClient, bool ClientApproved);

    // Versiones retiradas porque cambió la información (no porque Ingeniería las rechazara).
    private static readonly HashSet<string> RetiredReasons =
        [Npi.PrintCardRetiredByDesignReason, Npi.PrintCardRetiredByEngineeringReason, Npi.PrintCardRetiredByCommercialReason];

    private static string? PrintCardStepOf(PrintCardFacts? pc, bool loaded)
    {
        if (pc is null || loaded) return null;
        if (pc.ClientChanges) return "CambiosCliente";
        if (pc.Rejected) return pc.RejectionReason is { } reason && RetiredReasons.Contains(reason) ? "Rehacer" : "RechazadoIngenieria";
        if (pc.ClientApproved) return "Aprobado";
        if (!pc.EngineeringApproved) return "Ingenieria";
        return pc.SentToClient ? "Cliente" : "PorEnviar";
    }

    private static bool IsTerminal(LifecycleStatus status) =>
        status is LifecycleStatus.Completed or LifecycleStatus.Rejected or LifecycleStatus.Cancelled;
}
