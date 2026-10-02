using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace NpiManagement.Api.Extensions;

public static class ControllerBaseExtensions
{
    /// <summary>
    /// Construye un 400 ValidationProblemDetails a partir de un ValidationResult de
    /// FluentValidation, sin depender del paquete FluentValidation.AspNetCore (deprecado).
    /// </summary>
    public static ActionResult ValidationProblem(this ControllerBase controller, ValidationResult result)
    {
        foreach (var error in result.Errors)
            controller.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

        return controller.ValidationProblem(controller.ModelState);
    }
}
