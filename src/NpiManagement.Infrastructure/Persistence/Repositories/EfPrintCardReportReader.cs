using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.PrintCards;
using NpiManagement.Domain;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Datos del módulo PrintCards (2026-11-23). Trae sólo lo necesario de los NPIs con PrintCard: versiones
/// con aprobaciones, las entradas de bitácora que cuentan (asignación, envíos, cambios del cliente,
/// deshacer) y los eventos del cronómetro de Diseño. Cada colección va en su propia consulta (split).
/// </summary>
public sealed class EfPrintCardReportReader(NpiManagementDbContext dbContext) : IPrintCardReportReader
{
    private static readonly string[] Actions = ["DesignAssigned", "PrintCardSentToClient", "ClientChangesRequested", "ActionUndone"];

    public async Task<IReadOnlyList<PrintCardNpiSource>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Npis.AsNoTracking().AsSplitQuery()
            .Where(n => n.LifecycleStatus != LifecycleStatus.Draft && n.PrintCardVersions.Any())
            .Select(n => new
            {
                n.Id,
                n.NpiNumber,
                n.Capture.Cliente,
                n.Capture.DescripcionProducto,
                n.LifecycleStatus,
                n.AssignedDesignUserId,
                Versions = n.PrintCardVersions.Select(v => new
                {
                    v.VersionNumber, v.Code, v.UploadedByUserId, v.UploadedByName, v.UploadedAt, v.RejectedAt, v.RejectionReason,
                    Approvals = v.Approvals.Select(a => new { a.Role, a.At }).ToList(),
                }).ToList(),
                Events = n.AuditLog.Where(e => Actions.Contains(e.Action)).Select(e => new { e.OccurredAt, e.Action, e.Details }).ToList(),
                Timer = n.TimerEventLog.Where(t => t.Stage == TimerStage.Design).Select(t => new { t.OccurredAt, t.Action }).ToList(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new PrintCardNpiSource(
            r.Id, r.NpiNumber?.Value, r.Cliente ?? string.Empty, r.DescripcionProducto ?? string.Empty, r.LifecycleStatus, r.AssignedDesignUserId,
            r.Versions.Select(v => new PrintCardVersionSource(v.VersionNumber, v.Code, v.UploadedByUserId, v.UploadedByName, v.UploadedAt,
                v.RejectedAt, v.RejectionReason, v.Approvals.Select(a => new PrintCardApprovalSource(a.Role, a.At)).ToList())).ToList(),
            r.Events.Select(e => new PrintCardEventSource(e.OccurredAt, e.Action, e.Details)).ToList(),
            r.Timer.Select(t => new PrintCardTimerEventSource(t.OccurredAt, t.Action)).ToList()))
            .ToList();
    }
}
