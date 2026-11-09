using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NpiManagement.Api.Contracts.Feedback;
using NpiManagement.Api.Extensions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Reportes de problemas, sugerencias y dudas (docs/api-contract.md §10.1). Cualquier usuario
/// autenticado puede enviar y consultar el estado de los suyos; la bandeja completa, las capturas
/// y el cambio de estado son exclusivos de AdminTotal.
/// </summary>
[ApiController]
[Route("api/feedback")]
[Authorize]
public sealed class FeedbackController(IFeedbackService feedbackService) : ControllerBase
{
    /// <summary>
    /// El límite de tamaño corta la petición antes de leerla; el límite real de la captura lo
    /// aplica el servicio con la misma política que los adjuntos del NPI.
    /// </summary>
    [HttpPost]
    [AllowForReadOnlyRole]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [EnableRateLimiting("feedback")]
    public async Task<IActionResult> Submit([FromForm] SubmitFeedbackForm form, CancellationToken cancellationToken)
    {
        await using var screenshotStream = form.Screenshot?.OpenReadStream();
        var screenshot = form.Screenshot is null
            ? null
            : new FeedbackScreenshot(screenshotStream!, form.Screenshot.ContentType, form.Screenshot.Length);

        var request = new SubmitFeedbackRequest(
            form.Kind!.Value, form.Description, form.PageUrl, form.NpiId,
            form.UserAgent, form.ScreenSize, form.TechnicalDetails, screenshot);

        var id = await feedbackService.SubmitAsync(User.ToCurrentUser(), User.DisplayName(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new SubmitFeedbackResponse(id));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var reports = await feedbackService.ListMineAsync(User.ToCurrentUser().UserId, cancellationToken);
        return Ok(reports);
    }

    [HttpGet]
    [Authorize(Policy = "AdminTotal")]
    public async Task<IActionResult> List([FromQuery] FeedbackStatus? status, CancellationToken cancellationToken)
    {
        var reports = await feedbackService.ListAsync(status, cancellationToken);
        return Ok(reports);
    }

    [HttpGet("{id:guid}/screenshot")]
    [Authorize(Policy = "AdminTotal")]
    public async Task<IActionResult> Screenshot(Guid id, CancellationToken cancellationToken)
    {
        var file = await feedbackService.OpenScreenshotAsync(id, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "AdminTotal")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateFeedbackStatusRequest request, CancellationToken cancellationToken)
    {
        await feedbackService.UpdateStatusAsync(id, request.Status!.Value, request.AdminResponse, User.ToCurrentUser().UserId, cancellationToken);
        return NoContent();
    }
}
