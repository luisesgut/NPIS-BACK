using System.Security.Cryptography;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Attachments;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Entities;
using NpiManagement.Domain.Exceptions;

namespace NpiManagement.Application.Npis.Advance;

/// <summary>
/// Qué hizo una sincronización: el estado leído y los cambios que aplicó (vacío si nada). Applies=false:
/// el NPI es anterior a la conexión con Advance; se ve su estado pero no se actualiza solo.
/// </summary>
public sealed record AdvanceSyncResult(bool Enabled, AdvanceStatusDto? Status, IReadOnlyList<string> Changes, string? Error = null, bool Applies = true);

/// <summary>
/// Refleja en el NPI lo que dice Advance, el repositorio de PrintCards de toda la planta (2026-11-12):
///  1) PrintCard subido en Advance + aprobado por el cliente aquí → "PrintCard cargado en sistema".
///  2) Ya cargado: el PrintCard final es el PDF de Advance; si cambia, se trae y queda como final
///     (aunque ya haya PT o el NPI esté completado).
/// La ET se sigue marcando a mano con su botón (decisión de Luis, 2026-11-12); aquí sólo se muestra si ya está.
/// Sólo NPIs creados desde IAdvanceClient.AppliesFrom: los anteriores se quedan como están.
/// Lo hace a nombre de "Sistema (Advance)". Nada de esto bloquea: si Advance no responde, se reintenta.
/// </summary>
public sealed class AdvanceSyncService(IAdvanceClient advance, IFileStorage fileStorage, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public static readonly Actor SystemActor = new(Guid.Empty, "Sistema (Advance)", "Sistema (Advance)");
    public const string UpdatedFromAdvanceReason = "Actualizado desde Advance: el PDF del PrintCard cambió en Advance.";

    /// <summary>El código del PrintCard que se busca en Advance: el final, o el vigente si aún no se carga.</summary>
    /// <summary>¿El NPI es de los que se actualizan solos? (creado desde AppliesFrom).</summary>
    public static bool AppliesTo(IAdvanceClient advance, Npi npi) => advance.AppliesFrom is not { } from || npi.CreatedAt >= from;

    public static string? CodeOf(Npi npi) =>
        (npi.FinalPrintCardVersion ?? (npi.CurrentPrintCardVersion is { IsRejected: false } current ? current : null))?.Code?.Trim();

    /// <summary>Lee Advance y aplica los cambios al agregado (no guarda: lo hace quien llama).</summary>
    public async Task<AdvanceSyncResult> SyncAsync(Npi npi, CancellationToken cancellationToken)
    {
        if (!advance.IsEnabled) return new AdvanceSyncResult(false, null, []);
        if (!AppliesTo(advance, npi)) return new AdvanceSyncResult(true, null, [], Applies: false);
        var code = CodeOf(npi);
        if (string.IsNullOrWhiteSpace(code) || npi.LifecycleStatus is not (LifecycleStatus.Active or LifecycleStatus.Completed))
            return new AdvanceSyncResult(true, null, []);

        AdvanceStatusDto? status;
        try { status = await advance.GetStatusAsync(code, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new AdvanceSyncResult(true, null, [], "Advance no respondió.");
        }
        if (status is null || !status.ExisteEnCatalogo) return new AdvanceSyncResult(true, status, []);

        var changes = new List<string>();
        if (status.PrintCardSubido)
        {
            // 1) Cargado en sistema: sólo con la versión aprobada por el cliente (se respeta el flujo).
            if (npi.PrintCardLoadedAt is null && npi.LifecycleStatus == LifecycleStatus.Active
                && npi.CurrentPrintCardVersion is { IsRejected: false, IsFullyApproved: true }
                && Try(() => npi.LoadPrintCardIntoSystem(SystemActor, clock, calendar)))
                changes.Add("PrintCard cargado en sistema");

            // 2) El final es el PDF de Advance.
            if (npi.FinalPrintCardVersion is { } final && await TryUpdateFinalAsync(npi, final, code, cancellationToken))
                changes.Add("PrintCard final actualizado desde Advance");
        }

        if (changes.Count > 0)
        {
            var message = $"Advance: {string.Join(" · ", changes)} en el NPI {npi.NpiNumber?.Value} (PrintCard {code}).";
            Guid?[] people = [npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId];
            notifications.NotifyMany(people.OfType<Guid>(), message, npi.Id);
        }
        return new AdvanceSyncResult(true, status, changes);
    }

    private async Task<bool> TryUpdateFinalAsync(Npi npi, PrintCardVersion final, string code, CancellationToken cancellationToken)
    {
        AdvanceFile? file;
        try { file = await advance.DownloadPrintCardAsync(code, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { return false; }
        if (file is null || file.Content.Length == 0) return false;

        // Mismo contenido que el final: nada que hacer (se compara el archivo, no el nombre).
        var hash = Convert.ToHexString(SHA256.HashData(file.Content));
        var current = npi.Attachments.FirstOrDefault(a => a.Id == final.AttachmentId);
        if (current?.Sha256Hash is { } currentHash && string.Equals(currentHash, hash, StringComparison.OrdinalIgnoreCase)) return false;

        var name = FileNameSanitizer.Sanitize(string.IsNullOrWhiteSpace(file.FileName) ? $"{code}.pdf" : file.FileName);
        using var content = new MemoryStream(file.Content);
        var stored = await fileStorage.SaveAsync(content, name, cancellationToken);
        var attachment = npi.RegisterAttachment(SystemActor, clock, name, file.ContentType, stored.SizeBytes, stored.StorageKey, stored.Sha256Hash, AttachmentContext.PrintCard);
        return Try(() => npi.UpdateFinalPrintCard(SystemActor, clock, final.Code, attachment.Id, UpdatedFromAdvanceReason));
    }

    // Una regla del flujo que no se cumple (p. ej. un cierre en revisión) no es un error: se intenta en la siguiente vuelta.
    private static bool Try(Action action)
    {
        try { action(); return true; }
        catch (DomainException) { return false; }
    }
}

/// <summary>"Revisar Advance" desde el detalle: lee y aplica de inmediato, sin esperar la vuelta automática.</summary>
public sealed record SyncNpiWithAdvanceCommand(Guid NpiId, CurrentUser Actor);

public sealed class SyncNpiWithAdvanceCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, AdvanceSyncService sync)
{
    public async Task<AdvanceSyncResult> HandleAsync(SyncNpiWithAdvanceCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanView(command.Actor, NpiAuthorization.OwnershipOf(npi), npi.Capture.Cliente, npi.LifecycleStatus))
            throw new NotFoundException("Npi", command.NpiId);

        var result = await sync.SyncAsync(npi, cancellationToken);
        if (result.Changes.Count > 0) await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }
}

/// <summary>Estado del PrintCard del NPI en Advance para la tarjeta del detalle (sólo lee, no aplica nada).</summary>
public sealed record GetAdvanceStatusQuery(Guid NpiId, CurrentUser Actor);

public sealed class GetAdvanceStatusQueryHandler(INpiRepository repository, IAdvanceClient advance)
{
    public async Task<AdvanceSyncResult> HandleAsync(GetAdvanceStatusQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        if (!NpiAuthorization.CanView(query.Actor, NpiAuthorization.OwnershipOf(npi), npi.Capture.Cliente, npi.LifecycleStatus))
            throw new NotFoundException("Npi", query.NpiId);
        if (!advance.IsEnabled) return new AdvanceSyncResult(false, null, []);
        var code = AdvanceSyncService.CodeOf(npi);
        if (string.IsNullOrWhiteSpace(code)) return new AdvanceSyncResult(true, null, []);
        var applies = AdvanceSyncService.AppliesTo(advance, npi);
        try { return new AdvanceSyncResult(true, await advance.GetStatusAsync(code, cancellationToken), [], Applies: applies); }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new AdvanceSyncResult(true, null, [], "Advance no respondió.");
        }
    }
}
