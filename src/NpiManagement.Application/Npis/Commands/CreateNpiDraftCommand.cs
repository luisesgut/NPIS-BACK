using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 1 de workflow.md — Crear borrador. Actor siempre viene del token, nunca del body.</summary>
public sealed record CreateNpiDraftCommand(Guid ActorUserId, string ActorRole, string ActorName, NpiCaptureDto Capture, Priority Priority, Guid? PreAssignedEngineeringUserId);

public sealed class CreateNpiDraftCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Guid> HandleAsync(CreateNpiDraftCommand command, CancellationToken cancellationToken)
    {
        var actor = new Actor(command.ActorUserId, command.ActorRole, command.ActorName);
        var npi = Npi.CreateDraft(Guid.NewGuid(), actor, clock, command.Capture.ToDomain(), command.Priority, command.PreAssignedEngineeringUserId);

        repository.Add(npi);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return npi.Id;
    }
}
