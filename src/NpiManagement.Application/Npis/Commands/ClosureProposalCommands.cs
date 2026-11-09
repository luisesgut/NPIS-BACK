using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Application.Npis.Commands;

// Cierres con visto bueno: si un usuario normal (no admin) cierra el NPI —no factibilidad de
// Ingeniería o de Diseño, rechazo del cliente, rechazo total por insumos o rechazo por precio— el
// NPI no se cierra: queda una propuesta que el admin de su área (o AdminTotal) confirma o no
// autoriza. Los admins siguen cerrando directo. Comercial y las demás áreas sólo se enteran al
// confirmarse (los avisos de cierre son los mismos que en el cierre directo).

/// <summary>Cierra directo (admin) o deja la propuesta para el visto bueno (usuario normal), con sus avisos.</summary>
internal static class Closure
{
    public static async Task CloseOrProposeAsync(
        Npi npi, TerminalReason reason, CurrentUser user, string actorName, string? note, Guid? evidenceAttachmentId,
        Action<Actor> closeDirectly, IClock clock, INotificationService notifications, IUserDirectory userDirectory, CancellationToken cancellationToken)
    {
        var actor = new Actor(user.UserId, user.Role, actorName);
        if (NpiAuthorization.RequiresClosureApproval(user))
        {
            var proposal = npi.ProposeClosure(reason, actor, clock, note, evidenceAttachmentId);
            await NotifyReviewersAsync(npi, proposal, userDirectory, notifications, cancellationToken);
            return;
        }

        closeDirectly(actor);
        NotifyClosed(npi, reason, note, notifications);
    }

    /// <summary>Avisos de un NPI ya cerrado — los mismos se use el cierre directo o la confirmación del admin.</summary>
    public static void NotifyClosed(Npi npi, TerminalReason reason, string? note, INotificationService notifications)
    {
        var number = npi.NpiNumber?.Value;
        var engineeringAndDesign = new[] { npi.AssignedEngineeringUserId, npi.AssignedDesignUserId }.OfType<Guid>().Distinct().ToList();
        switch (reason)
        {
            case TerminalReason.NoFeasibility:
                notifications.Notify(npi.AssignedCommercialUserId, NotificationText.EngineeringNoFeasibility(number, note), npi.Id, NotificationChannel.BellAndEmail);
                break;
            case TerminalReason.DesignNoFeasibility:
                var message = NotificationText.DesignNoFeasibility(number, note ?? "");
                notifications.Notify(npi.AssignedCommercialUserId, message, npi.Id, NotificationChannel.BellAndEmail);
                if (npi.AssignedEngineeringUserId is { } engineerId)
                    notifications.Notify(engineerId, message, npi.Id);
                break;
            case TerminalReason.ClientRejection:
                notifications.NotifyMany(engineeringAndDesign, $"El cliente rechazó definitivamente el NPI {number}.", npi.Id, NotificationChannel.BellAndEmail);
                break;
            case TerminalReason.SuppliesRejectionTotal:
                notifications.NotifyMany(engineeringAndDesign, $"NPI {number} cerrado por rechazo total de insumos.", npi.Id, NotificationChannel.BellAndEmail);
                break;
            case TerminalReason.PriceRejection:
                notifications.NotifyMany(engineeringAndDesign, $"NPI {number}: cerrado — el cliente rechazó el precio.", npi.Id);
                break;
        }
    }

    /// <summary>Al admin del área y a AdminTotal: hay un cierre esperando su visto bueno.</summary>
    private static async Task NotifyReviewersAsync(Npi npi, ClosureProposal proposal, IUserDirectory userDirectory, INotificationService notifications, CancellationToken cancellationToken)
    {
        var areaAdmin = NpiAuthorization.AdminRoleOf(proposal.Area);
        var reviewers = (await userDirectory.ListAsync(null, cancellationToken))
            .Where(u => u.Status == UserStatus.Active && (u.Role == areaAdmin || u.Role == "AdminTotal"))
            .Select(u => u.Id);
        notifications.NotifyMany(reviewers,
            NotificationText.ClosureProposed(npi.NpiNumber?.Value, proposal.ProposedByName, Label(proposal.Reason), proposal.Note),
            npi.Id, NotificationChannel.BellAndEmail);
    }

    public static string Label(TerminalReason reason) => reason switch
    {
        TerminalReason.NoFeasibility => "no factibilidad de Ingeniería",
        TerminalReason.DesignNoFeasibility => "no factibilidad de Diseño",
        TerminalReason.ClientRejection => "rechazo definitivo del cliente",
        TerminalReason.SuppliesRejectionTotal => "rechazo total por falta de insumos",
        TerminalReason.PriceRejection => "rechazo por precio",
        _ => reason.ToString(),
    };

    public static async Task<Npi> LoadForReviewAsync(INpiRepository repository, Guid npiId, CurrentUser user, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(npiId, cancellationToken) ?? throw new NotFoundException("Npi", npiId);
        var pending = npi.PendingClosureProposal;
        // Sólo el admin del área del cierre (o AdminTotal) decide; a los demás el recurso "no existe".
        if (pending is null ? !NpiAuthorization.SeesAllNpis(user) : !NpiAuthorization.CanReviewClosure(user, pending.Area))
            throw new NotFoundException("Npi", npiId);
        return npi;
    }
}

/// <summary>El admin da luz verde: el NPI se cierra y se avisa como en el cierre directo.</summary>
public sealed record ConfirmClosureProposalCommand(Guid NpiId, CurrentUser Actor, string ActorName, string? Comment);

public sealed class ConfirmClosureProposalCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(ConfirmClosureProposalCommand command, CancellationToken cancellationToken)
    {
        var npi = await Closure.LoadForReviewAsync(repository, command.NpiId, command.Actor, cancellationToken);

        var proposal = npi.ConfirmClosureProposal(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.Comment);
        Closure.NotifyClosed(npi, proposal.Reason, proposal.Note, notifications);
        notifications.Notify(proposal.ProposedByUserId,
            $"Se autorizó el cierre del NPI {npi.NpiNumber?.Value} ({Closure.Label(proposal.Reason)}) que propusiste.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>El admin no autoriza el cierre: el NPI sigue y quien lo propuso recibe el porqué.</summary>
public sealed record DeclineClosureProposalCommand(Guid NpiId, CurrentUser Actor, string ActorName, string Comment);

public sealed class DeclineClosureProposalCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(DeclineClosureProposalCommand command, CancellationToken cancellationToken)
    {
        var npi = await Closure.LoadForReviewAsync(repository, command.NpiId, command.Actor, cancellationToken);

        var proposal = npi.DeclineClosureProposal(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, command.Comment);
        notifications.Notify(proposal.ProposedByUserId,
            NotificationText.ClosureDeclined(npi.NpiNumber?.Value, command.ActorName, proposal.DecisionNote ?? ""),
            npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Quien propuso el cierre lo retira (sólo esa persona).</summary>
public sealed record WithdrawClosureProposalCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class WithdrawClosureProposalCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(WithdrawClosureProposalCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (npi.PendingClosureProposal?.ProposedByUserId != command.Actor.UserId) throw new NotFoundException("Npi", command.NpiId);

        npi.WithdrawClosureProposal(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
