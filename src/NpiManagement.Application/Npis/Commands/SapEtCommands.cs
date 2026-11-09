using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain.Common;

namespace NpiManagement.Application.Npis.Commands;

/// <summary>Acción 18 de workflow.md — Registrar PT en SAP.</summary>
public sealed record RegisterSapPtCommand(Guid NpiId, CurrentUser Actor, string ActorName, string CodigoPT, string? PtName, string? SapTable = null);

public sealed class RegisterSapPtCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RegisterSapPtCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RegisterSapPt(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.CodigoPT, command.PtName, command.SapTable);
        notifications.Notify(npi.AssignedCommercialUserId, $"NPI {npi.NpiNumber?.Value}: PT {command.CodigoPT} en SAP — registre el número de Pedido.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 19 de workflow.md — Registrar Pedido en SAP.</summary>
public sealed record RegisterSapOrderCommand(Guid NpiId, CurrentUser Actor, string ActorName, string PedidoSapNumber);

public sealed class RegisterSapOrderCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(RegisterSapOrderCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsCommercial(command.Actor, npi.CreatedByUserId, npi.AssignedCommercialUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RegisterSapOrder(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar, command.PedidoSapNumber);
        if (npi.AssignedEngineeringUserId is { } engineerId)
            notifications.Notify(engineerId, $"NPI {npi.NpiNumber?.Value}: Pedido en SAP {command.PedidoSapNumber} registrado — puede crear la ET.", npi.Id, NotificationChannel.BellAndEmail);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 20 de workflow.md — Registrar ET Creada (hito separado de la carga en SisPro, decisión 9).</summary>
public sealed record RegisterEtCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class RegisterEtCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar)
{
    public async Task<NpiDetailDto> HandleAsync(RegisterEtCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.RegisterEt(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}

/// <summary>Acción 21 de workflow.md — Registrar ET cargada en SisPro. Puede completar el ciclo (decisión 4).</summary>
public sealed record LoadEtIntoSisproCommand(Guid NpiId, CurrentUser Actor, string ActorName);

public sealed class LoadEtIntoSisproCommandHandler(INpiRepository repository, IUnitOfWork unitOfWork, IClock clock, IBusinessCalendar calendar, INotificationService notifications)
{
    public async Task<NpiDetailDto> HandleAsync(LoadEtIntoSisproCommand command, CancellationToken cancellationToken)
    {
        var npi = await repository.GetByIdAsync(command.NpiId, cancellationToken) ?? throw new NotFoundException("Npi", command.NpiId);
        if (!NpiAuthorization.CanActAsEngineering(command.Actor, npi.AssignedEngineeringUserId)) throw new NotFoundException("Npi", command.NpiId);

        npi.LoadEtIntoSispro(new Actor(command.Actor.UserId, command.Actor.Role, command.ActorName), clock, calendar);

        if (npi.LifecycleStatus == Domain.LifecycleStatus.Completed)
            notifications.Notify(npi.AssignedCommercialUserId, $"El NPI {npi.NpiNumber?.Value} completó el ciclo exitosamente.", npi.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NpiMapper.ToDetail(npi, clock, calendar, repository.GetRowVersion(npi));
    }
}
