using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpiManagement.Application.Clients;

namespace NpiManagement.Api.Controllers;

public sealed record MergeClientNamesRequest(IReadOnlyList<string> From, string To);
public sealed record MergeClientNamesResponse(int UpdatedNpis);

/// <summary>
/// Nombres de cliente (2026-11-04). El listado lo usa cualquier usuario (sugerencias al capturar el
/// cliente); unificar nombres es sólo para AdminTotal e Ingeniería (política AreaIngenieria).
/// </summary>
[ApiController]
[Route("api/clients")]
[Authorize]
public sealed class ClientsController(ListClientNamesQueryHandler list, MergeClientNamesCommandHandler merge) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientNameDto>>> List(CancellationToken cancellationToken) =>
        Ok(await list.HandleAsync(cancellationToken));

    [HttpPost("merge")]
    [Authorize(Policy = "AreaIngenieria")]
    public async Task<ActionResult<MergeClientNamesResponse>> Merge(MergeClientNamesRequest request, CancellationToken cancellationToken) =>
        Ok(new MergeClientNamesResponse(await merge.HandleAsync(new MergeClientNamesCommand(request.From, request.To), cancellationToken)));
}
