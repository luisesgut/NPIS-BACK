using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Contracts.Npis;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Visto bueno de los cierres que propone un usuario normal (no factibilidad, rechazo del cliente,
/// por insumos o por precio). Confirmar y no autorizar los hace el admin del área del cierre o
/// AdminTotal; retirar, sólo quien lo propuso. Cada handler decide (404 si no le corresponde).
/// </summary>
[ApiController]
[Route("api/npis/{id:guid}/closure-proposal")]
[Authorize]
public sealed class NpiClosureProposalController(
    ConfirmClosureProposalCommandHandler confirmHandler,
    DeclineClosureProposalCommandHandler declineHandler,
    WithdrawClosureProposalCommandHandler withdrawHandler) : ControllerBase
{
    [HttpPost("confirm")]
    public Task<NpiDetailDto> Confirm(Guid id, ConfirmClosureProposalRequest request, CancellationToken cancellationToken) =>
        confirmHandler.HandleAsync(new ConfirmClosureProposalCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Comment), cancellationToken);

    [HttpPost("decline")]
    public Task<NpiDetailDto> Decline(Guid id, DeclineClosureProposalRequest request, CancellationToken cancellationToken) =>
        declineHandler.HandleAsync(new DeclineClosureProposalCommand(id, User.ToCurrentUser(), User.DisplayName(), request.Comment), cancellationToken);

    [HttpPost("withdraw")]
    public Task<NpiDetailDto> Withdraw(Guid id, CancellationToken cancellationToken) =>
        withdrawHandler.HandleAsync(new WithdrawClosureProposalCommand(id, User.ToCurrentUser(), User.DisplayName()), cancellationToken);
}
