using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain.Exceptions;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>
/// Eliminar un NPI para siempre (2026-11-03). Sólo AdminTotal, y tiene que escribir el folio (o
/// "BORRADOR" si nunca se envió) para confirmar.
/// </summary>
public sealed record DeleteNpiCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Confirmation);

public sealed class DeleteNpiCommandHandler(INpiRepository repository, INpiDeletion deletion)
{
    public async Task HandleAsync(DeleteNpiCommand command, CancellationToken cancellationToken)
    {
        if (command.Actor.Role != "AdminTotal") throw new NotFoundException("Npi", command.NpiId);
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);

        var expected = npi.NpiNumber?.Value ?? "BORRADOR";
        if (!string.Equals(command.Confirmation?.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            throw new NpiValidationException("delete-confirmation-mismatch", $"Para confirmar escribe exactamente {expected}.");

        await deletion.DeleteAsync(npi.Id, expected, command.Actor.UserId, command.ActorName, cancellationToken);
    }
}
