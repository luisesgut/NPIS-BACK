using System.ComponentModel.DataAnnotations;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;

namespace NpiManagement.Api.Contracts.Feedback;

/// <summary>
/// Reporte enviado como multipart/form-data (puede traer captura). Quién reporta y cuándo no viajan
/// aquí: salen del token y del reloj del servidor, igual que en el resto de la API.
/// </summary>
public sealed class SubmitFeedbackForm
{
    /// <summary>Nullable a propósito: sin esto, un formulario sin tipo se guardaría en silencio como "Bug" (el valor 0).</summary>
    [Required]
    public FeedbackKind? Kind { get; init; }

    [Required, MinLength(FeedbackLimits.DescriptionMinLength), MaxLength(FeedbackLimits.DescriptionMaxLength)]
    public string Description { get; init; } = string.Empty;

    public string? PageUrl { get; init; }
    public Guid? NpiId { get; init; }
    public string? UserAgent { get; init; }
    public string? ScreenSize { get; init; }

    /// <summary>Errores recientes que registró la app (pantallas que tronaron, llamadas rechazadas).</summary>
    public string? TechnicalDetails { get; init; }

    public IFormFile? Screenshot { get; init; }
}

public sealed record SubmitFeedbackResponse(Guid Id);

public sealed record UpdateFeedbackStatusRequest(
    [Required] FeedbackStatus? Status,
    [MaxLength(FeedbackLimits.AdminResponseMaxLength)] string? AdminResponse);
