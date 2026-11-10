using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Application.Npis.Queries;

/// <summary>
/// Un campo que cambió después del envío. RequestedBy*/Request*: la solicitud de cambios abierta en
/// ese momento (null = Comercial lo cambió por iniciativa propia). WasRequested: esa solicitud
/// marcó este campo en particular.
/// </summary>
public sealed record CaptureChangeDto(
    Guid Id, string Field, string Before, string After, string ChangedByName, DateTimeOffset ChangedAt,
    Guid? ChangeRequestId, bool WasRequested, string? RequestedByName, string? RequestedFromArea, DateTimeOffset? RequestedAt, string? RequestDescription);

/// <summary>
/// "Fase inicial" (captura tal como se envió la primera vez) + todos los cambios campo por campo.
/// InitialCapture es null en NPIs enviados antes de que existiera la bitácora.
/// </summary>
public sealed record CaptureHistoryDto(
    NpiCaptureDto? InitialCapture, string? InitialPriority, DateTimeOffset? FirstSubmittedAt, string? FirstSubmittedByName,
    IReadOnlyList<CaptureChangeDto> Changes);

public sealed record GetCaptureHistoryQuery(Guid NpiId, CurrentUser Actor);

public sealed class GetCaptureHistoryQueryHandler(INpiRepository repository, INpiCaptureHistoryStore history, IUserDirectory userDirectory)
{
    public async Task<CaptureHistoryDto> HandleAsync(GetCaptureHistoryQuery query, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(query.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", query.NpiId);
        var ownership = NpiAuthorization.OwnershipOf(npi);
        if (!NpiAuthorization.CanView(query.Actor, ownership, npi.Capture.Cliente, npi.LifecycleStatus)) throw new NotFoundException("Npi", query.NpiId);

        var changes = await history.ListAsync(npi.Id, cancellationToken);
        var requests = npi.ChangeRequests.ToDictionary(c => c.Id);

        string? submittedBy = null;
        if (npi.FirstSubmittedByUserId is { } submitterId)
        {
            var names = await userDirectory.GetNamesAsync([submitterId], cancellationToken);
            submittedBy = names.GetValueOrDefault(submitterId, GetNpiAssigneesQueryHandler.UnknownUserName);
        }

        // La prioridad inicial no está en la foto (sólo la captura): se reconstruye deshaciendo sus cambios.
        var firstPriorityChange = changes.FirstOrDefault(c => c.Field == "priority");
        var initialPriority = npi.InitialCaptureJson is null ? null : firstPriorityChange?.Before ?? npi.Priority.ToString();

        var initialCapture = CaptureDiff.FromSnapshot(npi.InitialCaptureJson);
        return new CaptureHistoryDto(
            initialCapture is null ? null : NpiCaptureDto.FromDomain(initialCapture),
            initialPriority,
            npi.FirstSubmittedAt,
            submittedBy,
            changes.Select(c =>
            {
                var request = c.ChangeRequestId is { } requestId ? requests.GetValueOrDefault(requestId) : null;
                return new CaptureChangeDto(
                    c.Id, c.Field, c.Before, c.After, c.ChangedByName, c.ChangedAt,
                    c.ChangeRequestId, c.WasRequested, request?.RequestedByName, request?.FromArea.ToString(), request?.RequestedAt, request?.Description);
            }).ToList());
    }
}
