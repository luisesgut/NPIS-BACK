using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Api.Controllers;

public sealed record EditInformationRequestBody(string Text, IReadOnlyList<Guid>? AddImageAttachmentIds = null);
public sealed record EditChangeRequestBody(string Description, IReadOnlyList<string>? FieldKeys, IReadOnlyDictionary<string, string>? FieldNotes = null, IReadOnlyList<Guid>? AddImageAttachmentIds = null);

/// <summary>
/// Editar o retirar una solicitud ya enviada (2026-11-04), mientras nadie la ha atendido. Cada
/// handler revisa que quien la toca sea del área que la pidió (404 si no le corresponde).
/// </summary>
[ApiController]
[Route("api/npis/{id:guid}")]
[Authorize]
public sealed class NpiRequestEditController(
    EditInformationRequestCommandHandler editInformation,
    WithdrawInformationRequestCommandHandler withdrawInformation,
    EditChangeRequestCommandHandler editChange,
    WithdrawChangeRequestCommandHandler withdrawChange) : ControllerBase
{
    [HttpPost("information-requests/{requestId:guid}/edit")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> EditInformation(Guid id, Guid requestId, EditInformationRequestBody body, CancellationToken cancellationToken) =>
        editInformation.HandleAsync(new EditInformationRequestCommand(id, requestId, User.ToCurrentUser(), User.DisplayName(), body.Text, body.AddImageAttachmentIds), cancellationToken);

    [HttpPost("information-requests/{requestId:guid}/withdraw")]
    [Authorize(Policy = "AreaDiseno")]
    public Task<NpiDetailDto> WithdrawInformation(Guid id, Guid requestId, CancellationToken cancellationToken) =>
        withdrawInformation.HandleAsync(new WithdrawInformationRequestCommand(id, requestId, User.ToCurrentUser(), User.DisplayName()), cancellationToken);

    [HttpPost("change-requests/{requestId:guid}/edit")]
    public Task<NpiDetailDto> EditChange(Guid id, Guid requestId, EditChangeRequestBody body, CancellationToken cancellationToken) =>
        editChange.HandleAsync(new EditChangeRequestCommand(id, requestId, User.ToCurrentUser(), User.DisplayName(), body.Description, body.FieldKeys ?? [], body.FieldNotes, body.AddImageAttachmentIds), cancellationToken);

    [HttpPost("change-requests/{requestId:guid}/withdraw")]
    public Task<NpiDetailDto> WithdrawChange(Guid id, Guid requestId, CancellationToken cancellationToken) =>
        withdrawChange.HandleAsync(new WithdrawChangeRequestCommand(id, requestId, User.ToCurrentUser(), User.DisplayName()), cancellationToken);
}
