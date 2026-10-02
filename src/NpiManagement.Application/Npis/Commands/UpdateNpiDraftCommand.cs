using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 2 de workflow.md — Editar borrador (o reenvío pendiente, ver Npi.UpdateDraft).</summary>
public sealed record UpdateNpiDraftCommand(Guid NpiId, CurrentUser Actor, NpiCaptureDto Capture, Priority Priority, byte[] ExpectedRowVersion);

public sealed class UpdateNpiDraftCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(UpdateNpiDraftCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken)
            ?? throw new NotFoundException("Npi", command.NpiId);

        if (!NpiAuthorization.CanManageDraft(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId))
            throw new NotFoundException("Npi", command.NpiId);

        // El cliente declara qué versión creía tener (obtenida de un GET previo): si alguien más
        // ya guardó cambios desde entonces, SaveChangesAsync lanza DbUpdateConcurrencyException →
        // 409 (ApiExceptionHandler) — detecta ediciones perdidas entre requests, no sólo carreras
        // dentro de esta misma request (ver Fase 8, docs/api-contract.md §0).
        repository.SetExpectedRowVersion(npi, command.ExpectedRowVersion);

        npi.UpdateDraft(command.Capture.ToDomain(), command.Priority);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
