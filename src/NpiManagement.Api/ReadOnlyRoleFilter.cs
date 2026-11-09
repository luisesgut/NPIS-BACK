using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NpiManagement.Domain;

namespace NpiManagement.Api;

/// <summary>Acciones que el rol Director (sólo consulta) sí puede hacer: su sesión, marcar avisos como leídos y reportar un problema.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowForReadOnlyRoleAttribute : Attribute;

/// <summary>
/// El rol Director es de sólo consulta (2026-11-09): ve todos los NPIs y el dashboard de dirección,
/// pero cualquier petición que modifique algo se rechaza con 403, salvo las marcadas con
/// <see cref="AllowForReadOnlyRoleAttribute"/>. Una sola regla aquí, en vez de revisarlo en cada acción.
/// </summary>
public sealed class ReadOnlyRoleFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var readsOnly = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method);
        if (!readsOnly
            && http.User.FindFirstValue(ClaimTypes.Role) == ApplicationRoles.Director
            && !context.ActionDescriptor.EndpointMetadata.OfType<AllowForReadOnlyRoleAttribute>().Any())
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Sólo consulta",
                Detail = "El rol Director es de sólo consulta: puede ver todo, pero no hacer cambios.",
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}
