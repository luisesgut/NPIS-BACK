using System.Linq.Expressions;
using NpiManagement.Domain;

namespace NpiManagement.Application.Authorization;

/// <summary>Identidad resuelta desde el token (nunca desde el body de la petición) — ver docs/api-contract.md §12.</summary>
/// <param name="ViewClients">Clientes cuyos NPIs puede CONSULTAR aunque no sean suyos ("Clientes que puede ver", 2026-11-10).</param>
/// <param name="WorkClients">De ésos, los que además puede TRABAJAR ("Ver y trabajar"): actúa como el equipo de su área.</param>
public sealed record CurrentUser(Guid UserId, string Role, AreaType Area, IReadOnlySet<string>? ViewClients = null, IReadOnlySet<string>? WorkClients = null);

/// <summary>Snapshot mínimo de propiedad/asignación de un Npi, suficiente para decidir autorización sin cargar el agregado completo.</summary>
/// <param name="Participants">Equipo del NPI (2026-11-10): actúan como el responsable de su área.</param>
/// <param name="Cliente">Para "Ver y trabajar" por cliente; con <paramref name="Lifecycle"/> (los borradores no se comparten).</param>
public sealed record NpiOwnership(Guid CreatedByUserId, Guid AssignedCommercialUserId, Guid? AssignedEngineeringUserId, Guid? AssignedDesignUserId, IReadOnlyCollection<NpiParticipantRef>? Participants = null, string? Cliente = null, LifecycleStatus? Lifecycle = null);

public sealed record NpiParticipantRef(Guid UserId, AreaType Area);

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

        var own = user.Role switch
        {
            "UsuarioComercial" => npi.CreatedByUserId == user.UserId || npi.AssignedCommercialUserId == user.UserId,
            "UsuarioIngenieria" => npi.AssignedEngineeringUserId == user.UserId,
            "UsuarioDiseno" => npi.AssignedDesignUserId == user.UserId,
            _ => false,
        };
        return own
            || (UserAreaOf(user) is { } area && npi.Participants?.Any(p => p.UserId == user.UserId && p.Area == area) == true)
            || (npi.Lifecycle is { } lifecycle && WorksOnClient(user, npi.Cliente, lifecycle));
    }

    /// <summary>
    /// "Ver y trabajar" (2026-11-10): AdminTotal le compartió el cliente para operar sus NPIs como el
    /// equipo de su área. Sólo usuarios normales y nunca borradores (siguen siendo del vendedor).
    /// </summary>
    public static bool WorksOnClient(CurrentUser user, string? cliente, LifecycleStatus lifecycle) =>
        lifecycle != LifecycleStatus.Draft && UserAreaOf(user) is not null
        && user.WorkClients is { Count: > 0 } clients && !string.IsNullOrWhiteSpace(cliente)
        && clients.Contains(Users.ClientViewGrants.Normalize(cliente));

    /// <summary>Ownership del agregado, con su equipo.</summary>
    public static NpiOwnership OwnershipOf(Npi npi) =>
        new(npi.CreatedByUserId, npi.AssignedCommercialUserId, npi.AssignedEngineeringUserId, npi.AssignedDesignUserId,
            npi.Participants.Select(p => new NpiParticipantRef(p.UserId, p.Area)).ToList(), npi.Capture.Cliente, npi.LifecycleStatus);

    /// <summary>
    /// Área de un usuario normal. El equipo sólo cuenta para ellos: los admins ya ven y operan todo,
    /// y si a alguien le cambian el rol a otra área, su lugar en el equipo deja de darle permisos.
    /// </summary>
    public static AreaType? UserAreaOf(CurrentUser user) => user.Role switch
    {
        "UsuarioComercial" => AreaType.Comercial,
        "UsuarioIngenieria" => AreaType.Ingenieria,
        "UsuarioDiseno" => AreaType.Diseno,
        _ => null,
    };

    private static bool IsTeamMember(CurrentUser user, Npi npi, AreaType area) =>
        UserAreaOf(user) == area && (npi.IsParticipant(user.UserId, area) || WorksOnClient(user, npi.Capture.Cliente, npi.LifecycleStatus));

    /// <summary>Lo puede trabajar sólo por "Ver y trabajar" (no es responsable ni del equipo): el front lo trata como equipo.</summary>
    public static bool WorksOnlyByClient(CurrentUser user, Npi npi) =>
        UserAreaOf(user) is { } area && !npi.IsResponsible(user.UserId, area) && !npi.IsParticipant(user.UserId, area)
        && WorksOnClient(user, npi.Capture.Cliente, npi.LifecycleStatus);

    /// <summary>Agregar o quitar gente del equipo de un área: su admin, AdminTotal o el responsable de esa área.</summary>
    public static bool CanManageParticipants(CurrentUser user, Npi npi, AreaType area) =>
        CanAssign(user, area) || (UserAreaOf(user) == area && npi.IsResponsible(user.UserId, area));

    /// <summary>
    /// Consulta (detalle, historial, archivos, listado): lo de <see cref="CanAccess"/> más los NPIs de
    /// los clientes que AdminTotal le compartió. Nunca para escribir: eso sigue siendo CanAccess.
    /// </summary>
    /// Los borradores nunca se comparten: todavía son del vendedor que los captura.
    public static bool CanView(CurrentUser user, NpiOwnership npi, string? cliente, LifecycleStatus lifecycle) =>
        CanAccess(user, npi) || (lifecycle != LifecycleStatus.Draft && IsSharedClient(user, cliente));

    /// <summary>Lo ve sólo porque su cliente está compartido (el front lo muestra como "sólo consulta").</summary>
    public static bool IsViewOnly(CurrentUser user, NpiOwnership npi, string? cliente, LifecycleStatus lifecycle) =>
        !CanAccess(user, npi) && lifecycle != LifecycleStatus.Draft && IsSharedClient(user, cliente);

    private static bool IsSharedClient(CurrentUser user, string? cliente) =>
        user.ViewClients is { Count: > 0 } clients && !string.IsNullOrWhiteSpace(cliente)
        && clients.Contains(Users.ClientViewGrants.Normalize(cliente));

    /// <summary>Misma regla que <see cref="CanView"/>, como Expression para el listado.</summary>
    public static Expression<Func<Npi, bool>> BuildViewPredicate(CurrentUser user)
    {
        if (user.ViewClients is not { Count: > 0 } || AdminRoles.Contains(user.Role) || IsDirector(user))
            return BuildAccessPredicate(user);

        var id = user.UserId;
        var clients = user.ViewClients.ToList();
        return user.Role switch
        {
            "UsuarioComercial" => npi => npi.CreatedByUserId == id || npi.AssignedCommercialUserId == id || npi.Participants.Any(p => p.UserId == id && p.Area == AreaType.Comercial) || (npi.LifecycleStatus != LifecycleStatus.Draft && clients.Contains(npi.Capture.Cliente)),
            "UsuarioIngenieria" => npi => npi.AssignedEngineeringUserId == id || npi.Participants.Any(p => p.UserId == id && p.Area == AreaType.Ingenieria) || (npi.LifecycleStatus != LifecycleStatus.Draft && clients.Contains(npi.Capture.Cliente)),
            "UsuarioDiseno" => npi => npi.AssignedDesignUserId == id || npi.Participants.Any(p => p.UserId == id && p.Area == AreaType.Diseno) || (npi.LifecycleStatus != LifecycleStatus.Draft && clients.Contains(npi.Capture.Cliente)),
            _ => _ => false,
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

        var id = user.UserId;
        // "Ver y trabajar": los NPIs (no borradores) de esos clientes también son suyos para operar.
        var work = user.WorkClients?.ToList() ?? [];
        return user.Role switch
        {
            "UsuarioComercial" => npi => npi.CreatedByUserId == id || npi.AssignedCommercialUserId == id || npi.Participants.Any(p => p.UserId == id && p.Area == AreaType.Comercial) || (npi.LifecycleStatus != LifecycleStatus.Draft && work.Contains(npi.Capture.Cliente)),
            "UsuarioIngenieria" => npi => npi.AssignedEngineeringUserId == id || npi.Participants.Any(p => p.UserId == id && p.Area == AreaType.Ingenieria) || (npi.LifecycleStatus != LifecycleStatus.Draft && work.Contains(npi.Capture.Cliente)),
            "UsuarioDiseno" => npi => npi.AssignedDesignUserId == id || npi.Participants.Any(p => p.UserId == id && p.Area == AreaType.Diseno) || (npi.LifecycleStatus != LifecycleStatus.Draft && work.Contains(npi.Capture.Cliente)),
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

    // Versiones sobre el agregado: además del responsable, su equipo del área (2026-11-10).
    public static bool CanManageDraft(CurrentUser user, Npi npi) =>
        CanManageDraft(user, npi.CreatedByUserId, npi.AssignedCommercialUserId) || IsTeamMember(user, npi, AreaType.Comercial);

    public static bool CanActAsCommercial(CurrentUser user, Npi npi) => CanManageDraft(user, npi);

    public static bool CanActAsEngineering(CurrentUser user, Npi npi) =>
        CanActAsEngineering(user, npi.AssignedEngineeringUserId) || IsTeamMember(user, npi, AreaType.Ingenieria);

    public static bool CanActAsDesign(CurrentUser user, Npi npi) =>
        CanActAsDesign(user, npi.AssignedDesignUserId) || IsTeamMember(user, npi, AreaType.Diseno);
}
