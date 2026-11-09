using System.Linq.Expressions;
using NpiManagement.Domain;

namespace NpiManagement.Application.Authorization;

/// <summary>Identidad resuelta desde el token (nunca desde el body de la petición) — ver docs/api-contract.md §12.</summary>
public sealed record CurrentUser(Guid UserId, string Role, AreaType Area);

/// <summary>Snapshot mínimo de propiedad/asignación de un Npi, suficiente para decidir autorización sin cargar el agregado completo.</summary>
public sealed record NpiOwnership(Guid CreatedByUserId, Guid AssignedCommercialUserId, Guid? AssignedEngineeringUserId, Guid? AssignedDesignUserId);

/// <summary>
/// Política de autorización dependiente del recurso para el agregado Npi (docs/authorization-matrix.md).
/// Reglas:
///  - AdminTotal y los administradores de área (Comercial/Ingeniería/Diseño) tienen acceso total
///    a cualquier NPI, asignado o no — ninguno de ellos está limitado a "su área" porque todo
///    NPI participa potencialmente en las tres áreas.
///  - Un usuario normal sólo accede a los NPIs propios/asignados (decisión 11 del propietario):
///    UsuarioComercial → creador o asignado como comercial; UsuarioIngenieria/UsuarioDiseno →
///    únicamente si están asignados como tal.
/// No depende de EF Core, HTTP ni Identity — 100% lógica pura, comprobable en Application.Tests.
/// No confía en "role"/"area" enviados por el cliente: <see cref="CurrentUser"/> siempre se
/// construye a partir de los claims del token ya validado.
/// </summary>
public static class NpiAuthorization
{
    private static readonly HashSet<string> AdminRoles =
        ["AdminTotal", "AdminComercial", "AdminIngenieria", "AdminDiseno"];

    /// <summary>Los administradores (total y de área) y el Director (sólo consulta) ven todos los NPIs.</summary>
    public static bool SeesAllNpis(CurrentUser user) => AdminRoles.Contains(user.Role) || IsDirector(user);

    /// <summary>Dirección: ve todo, no opera nada (las escrituras las bloquea ReadOnlyRoleFilter en la API).</summary>
    public static bool IsDirector(CurrentUser user) => user.Role == ApplicationRoles.Director;

    /// <summary>Alcance único usado tanto para lectura (detalle/listado/comentarios) como para operar el recurso — ver authorization-matrix.md §1-2.</summary>
    public static bool CanAccess(CurrentUser user, NpiOwnership npi)
    {
        if (AdminRoles.Contains(user.Role) || IsDirector(user)) return true;

        return user.Role switch
        {
            "UsuarioComercial" => npi.CreatedByUserId == user.UserId || npi.AssignedCommercialUserId == user.UserId,
            "UsuarioIngenieria" => npi.AssignedEngineeringUserId == user.UserId,
            "UsuarioDiseno" => npi.AssignedDesignUserId == user.UserId,
            _ => false,
        };
    }

    /// <summary>Sólo AdminTotal o el admin del área correspondiente pueden asignar (acciones 4/5 de workflow.md).</summary>
    public static bool CanAssign(CurrentUser user, AreaType area) =>
        user.Role == "AdminTotal" || user.Role == AdminRoleOf(area);

    public static string AdminRoleOf(AreaType area) => area switch
    {
        AreaType.Comercial => "AdminComercial",
        AreaType.Ingenieria => "AdminIngenieria",
        AreaType.Diseno => "AdminDiseno",
        _ => "__none__",
    };

    /// <summary>
    /// Un usuario normal no cierra un NPI por sí solo: su cierre (no factibilidad, rechazo del cliente,
    /// por insumos o por precio) queda como propuesta hasta que el admin de su área da el visto bueno.
    /// </summary>
    public static bool RequiresClosureApproval(CurrentUser user) =>
        user.Role is "UsuarioComercial" or "UsuarioIngenieria" or "UsuarioDiseno";

    /// <summary>Confirmar o no autorizar una propuesta de cierre: el admin de esa área o AdminTotal (misma regla que asignar).</summary>
    public static bool CanReviewClosure(CurrentUser user, AreaType area) => CanAssign(user, area);

    /// <summary>
    /// Editar un borrador (acción 2 de workflow.md) es más estricto que CanAccess: sólo
    /// Comercial (propio/asignado) o admins de Comercial/Total — un ingeniero/diseñador asignado
    /// puede *ver* el NPI (CanAccess) pero no editar su captura.
    /// </summary>
    public static bool CanManageDraft(CurrentUser user, Guid createdByUserId, Guid assignedCommercialUserId)
    {
        if (user.Role is "AdminTotal" or "AdminComercial") return true;
        return user.Role == "UsuarioComercial" && (createdByUserId == user.UserId || assignedCommercialUserId == user.UserId);
    }

    /// <summary>
    /// Misma regla que <see cref="CanAccess"/>, expresada como Expression para que Infrastructure
    /// pueda traducirla a SQL (WHERE) al listar — nunca se carga todo y se filtra en memoria.
    /// Debe mantenerse en sincronía con CanAccess (ver prueba de consistencia en Application.Tests).
    /// </summary>
    public static Expression<Func<Npi, bool>> BuildAccessPredicate(CurrentUser user)
    {
        if (AdminRoles.Contains(user.Role) || IsDirector(user))
            return _ => true;

        return user.Role switch
        {
            "UsuarioComercial" => npi => npi.CreatedByUserId == user.UserId || npi.AssignedCommercialUserId == user.UserId,
            "UsuarioIngenieria" => npi => npi.AssignedEngineeringUserId == user.UserId,
            "UsuarioDiseno" => npi => npi.AssignedDesignUserId == user.UserId,
            _ => _ => false,
        };
    }

    /// <summary>
    /// Acciones de Ingeniería (factibilidad, PT/ET en SAP, rechazos por insumos, etc. — ver
    /// workflow.md grupos 3/5/6): AdminTotal, AdminIngenieria, o el propio ingeniero asignado.
    /// </summary>
    public static bool CanActAsEngineering(CurrentUser user, Guid? assignedEngineeringUserId)
    {
        if (user.Role is "AdminTotal" or "AdminIngenieria") return true;
        return user.Role == "UsuarioIngenieria" && assignedEngineeringUserId == user.UserId;
    }

    /// <summary>
    /// Acciones de Diseño (factibilidad, Artes Validadas, PrintCard, solicitudes de información —
    /// ver workflow.md grupos 3/4/7): AdminTotal, AdminDiseno, o el propio diseñador asignado.
    /// </summary>
    public static bool CanActAsDesign(CurrentUser user, Guid? assignedDesignUserId)
    {
        if (user.Role is "AdminTotal" or "AdminDiseno") return true;
        return user.Role == "UsuarioDiseno" && assignedDesignUserId == user.UserId;
    }

    /// <summary>
    /// Acciones de Comercial en nombre propio o del cliente (envío/aprobación de PrintCard al
    /// cliente, SAP, insumos — ver workflow.md grupos 4/5/6/7 y decisión 8): AdminTotal,
    /// AdminComercial, o el usuario comercial propio/asignado — misma regla que CanManageDraft.
    /// </summary>
    public static bool CanActAsCommercial(CurrentUser user, Guid createdByUserId, Guid assignedCommercialUserId) =>
        CanManageDraft(user, createdByUserId, assignedCommercialUserId);
}
