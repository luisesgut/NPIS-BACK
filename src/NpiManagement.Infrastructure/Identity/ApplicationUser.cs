using Microsoft.AspNetCore.Identity;
using NpiManagement.Domain;

namespace NpiManagement.Infrastructure.Identity;

/// <summary>
/// Usuario de ASP.NET Core Identity. Los 7 roles funcionales (AdminTotal, AdminComercial, ...)
/// se implementan como roles nativos de Identity (AspNetRoles/AspNetUserRoles) — ver
/// docs/domain-model.md §8 y ADR-020. "Area" es una columna propia porque es una regla de
/// negocio (usada por las políticas de autorización), no un detalle de autenticación.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Mientras <see cref="Status"/> sea <c>Pending</c> esto es sólo el área que el usuario dijo
    /// que le corresponde al registrarse: una sugerencia, no un dato de autoridad. El
    /// administrador la confirma o la corrige al aprobarlo, y a partir de ahí sí gobierna la
    /// autorización.
    /// </summary>
    public AreaType Area { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Pending;

    /// <summary>Cuándo se creó la cuenta — ordena la bandeja de solicitudes pendientes.</summary>
    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>Quién la aprobó y cuándo (null mientras nadie la haya aprobado).</summary>
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// Director de Comercial del vendedor (2026-11-18, "Mi equipo"): un AdminComercial. El director ve
    /// y trabaja los NPIs de su equipo, y el vendedor los de su director (no los de sus compañeros).
    /// Lo asigna AdminTotal en Usuarios; null = sin director.
    /// </summary>
    public Guid? DirectorUserId { get; set; }

    /// <summary>
    /// Atajo de lectura para las comprobaciones de "¿puede operar?". No es columna: se deriva de
    /// <see cref="Status"/>, para que no existan dos fuentes de verdad que puedan discrepar.
    /// </summary>
    public bool IsActive => Status == UserStatus.Active;
}
