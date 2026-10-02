using System.ComponentModel.DataAnnotations;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain;

namespace NpiManagement.Api.Contracts.Npis;

public sealed record CreateNpiDraftRequest(
    [Required] NpiCaptureDto Capture,
    Priority Priority = Priority.Media,
    Guid? PreAssignedEngineeringUserId = null);

public sealed record UpdateNpiDraftRequest(
    [Required] NpiCaptureDto Capture,
    Priority Priority,
    [Required] byte[] ExpectedRowVersion);
