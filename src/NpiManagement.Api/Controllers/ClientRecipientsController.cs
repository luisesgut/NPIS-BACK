using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.ClientRecipients;

namespace NpiManagement.Api.Controllers;

/// <summary>Libreta de correos del vendedor por cliente, para armar el borrador de Outlook de "Enviar al cliente".</summary>
[ApiController]
[Route("api/client-recipients")]
[Authorize(Policy = "AreaComercial")]
public sealed class ClientRecipientsController(ClientRecipientsService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ClientRecipientDto>> List([FromQuery] string cliente, CancellationToken cancellationToken) =>
        service.ListAsync(User.ToCurrentUser().UserId, cliente, cancellationToken);

    [HttpPut]
    public Task<IReadOnlyList<ClientRecipientDto>> Save(SaveClientRecipientsRequest request, CancellationToken cancellationToken) =>
        service.SaveAsync(User.ToCurrentUser().UserId, request.Cliente, request.Recipients, cancellationToken);
}

public sealed record SaveClientRecipientsRequest(string Cliente, IReadOnlyList<ClientRecipientDto> Recipients);
