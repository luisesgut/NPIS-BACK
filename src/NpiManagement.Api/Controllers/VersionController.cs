using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NpiManagement.Api.Controllers;

/// <summary>
/// Versión publicada del API (2026-11-20, v1.1.0). El front la muestra junto a la suya en el menú
/// lateral y avisa si no coinciden (se publicó uno sin el otro). Sin datos sensibles: abierta.
/// </summary>
[ApiController]
[Route("api/version")]
[AllowAnonymous]
public sealed class VersionController : ControllerBase
{
    private static readonly string Version = Read();

    [HttpGet]
    public ActionResult<object> Get() => Ok(new { version = Version });

    /// <summary>La de `&lt;Version&gt;` del csproj, sin el "+commit" que .NET agrega a la informativa.</summary>
    internal static string Read()
    {
        var info = typeof(VersionController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(info) ? "0.0.0" : info.Split('+')[0];
    }
}
