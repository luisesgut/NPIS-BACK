using NpiManagement.Application.Authorization;
using NpiManagement.Application.Dashboard;
using NpiManagement.Domain;

namespace NpiManagement.Application.Npis.Turns;

/// <summary>
/// Filtros "¿Qué necesita?" y "¿Dónde está?" de los paneles (2026-11-09). Salen de los mismos
/// turnos que "Tu turno" y de las mismas metas que el Dashboard, para que un NPI "atrasado" lo sea
/// igual en el panel que en el Dashboard. Sólo aplican a NPIs activos (los borradores no tienen reloj).
/// </summary>
public static class NpiSituation
{
    /// <summary>Le toca a tu área (a ti, si no eres admin).</summary>
    public const string Mine = "mine";
    /// <summary>Lo que le toca a tu área ya pasó su meta.</summary>
    public const string Late = "late";
    /// <summary>Tu área ya hizo lo suyo: espera a otra área.</summary>
    public const string Waiting = "waiting";
    /// <summary>Comercial tiene algo que corregir o contestar (cambios, preguntas, insumos, cotización).</summary>
    public const string Changes = "changes";
    /// <summary>Ingeniería debe reconfirmar su factibilidad.</summary>
    public const string Reconfirmation = "reconfirmation";

    public static readonly IReadOnlyList<string> All = [Mine, Late, Waiting, Changes, Reconfirmation];
}

/// <summary>Etapas que ve el usuario (agrupan las del Dashboard).</summary>
public static class NpiStageGroup
{
    public const string Factibilidad = "factibilidad";
    public const string PrintCard = "printcard";
    public const string Cliente = "cliente";
    public const string Alta = "alta";
    public const string Pedido = "pedido";

    public static readonly IReadOnlyList<string> All = [Factibilidad, PrintCard, Cliente, Alta, Pedido];

    public static string? FromDashboardStage(string stage) => stage switch
    {
        "ingenieria" or "diseno" => Factibilidad,
        "envio_cliente" => PrintCard,
        "aprobacion_cliente" => Cliente,
        "alta_sistemas" => Alta,
        "orden_venta" or "et_sispro" => Pedido,
        _ => null, // "correcciones" es una situación (Changes), no una etapa.
    };
}

public sealed record NpiSituationFlags(IReadOnlySet<string> Situations, IReadOnlySet<string> Stages);

/// <summary>Contadores para los chips de los paneles.</summary>
public sealed record NpiSituationCountsDto(int Active, IReadOnlyDictionary<string, int> Situations, IReadOnlyDictionary<string, int> Stages);

public sealed class NpiSituationResolver(INpiTurnReader reader, DashboardGoals goals)
{
    private const double BusinessDayMs = 9 * 60 * 60 * 1000;

    /// <summary>
    /// Situación y etapa de cada NPI activo que el usuario puede ver. <paramref name="area"/> es el
    /// área del panel; sólo cuenta para AdminTotal y Director (los demás usan siempre la suya).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, NpiSituationFlags>> ResolveAsync(CurrentUser actor, AreaType? area, CancellationToken cancellationToken)
    {
        var turns = await reader.ListAsync(NpiAuthorization.BuildAccessPredicate(actor), cancellationToken);
        // "Te toca" igual que "Tu turno" (TurnScope): el admin de Comercial sólo cuenta lo suyo.
        IReadOnlySet<Guid>? own = TurnScope.IsRestricted(actor)
            ? (await reader.ListAsync(TurnScope.For(actor), cancellationToken)).Select(t => t.NpiId).ToHashSet()
            : null;
        return Resolve(turns, actor, area, goals, own);
    }

    public async Task<NpiSituationCountsDto> CountAsync(CurrentUser actor, AreaType? area, CancellationToken cancellationToken)
    {
        var flags = await ResolveAsync(actor, area, cancellationToken);
        return new NpiSituationCountsDto(
            flags.Count,
            NpiSituation.All.ToDictionary(s => s, s => flags.Values.Count(f => f.Situations.Contains(s))),
            NpiStageGroup.All.ToDictionary(s => s, s => flags.Values.Count(f => f.Stages.Contains(s))));
    }

    /// <summary>Puro (sin base de datos) para poder probarlo.</summary>
    public static IReadOnlyDictionary<Guid, NpiSituationFlags> Resolve(
        IReadOnlyList<TurnItemDto> turns, CurrentUser actor, AreaType? area, DashboardGoals goals, IReadOnlySet<Guid>? ownNpis = null)
    {
        var myArea = actor.Area == AreaType.Total ? area : actor.Area;
        var isAdmin = NpiAuthorization.SeesAllNpis(actor);
        var result = new Dictionary<Guid, NpiSituationFlags>();

        // Los borradores no tienen meta (ForAction == null): no entran.
        foreach (var npi in turns.Where(t => goals.ForAction(t.Action) is not null).GroupBy(t => t.NpiId))
        {
            var situations = new HashSet<string>();
            var stages = new HashSet<string>();
            var mineTurns = npi
                .Where(t => myArea is null || t.Area == myArea.ToString())
                .Where(t => isAdmin || !NpiTurns.AdminOnly.Contains(t.Action))
                .ToList();

            // Lo que no es suyo (admin de Comercial) no es "te toca" ni "esperando": sólo cuenta para la etapa.
            if (ownNpis is not null && !ownNpis.Contains(npi.Key)) mineTurns.Clear();
            else if (mineTurns.Count > 0) situations.Add(NpiSituation.Mine);
            else if (myArea is not null) situations.Add(NpiSituation.Waiting);

            if (mineTurns.Any(t => IsLate(t, goals))) situations.Add(NpiSituation.Late);

            foreach (var t in npi)
            {
                var (stage, _) = goals.ForAction(t.Action)!.Value;
                if (stage == "correcciones") situations.Add(NpiSituation.Changes);
                if (NpiStageGroup.FromDashboardStage(stage) is { } group) stages.Add(group);
                if (t.Action == NpiTurns.ReconfirmFeasibility) situations.Add(NpiSituation.Reconfirmation);
            }
            result[npi.Key] = new NpiSituationFlags(situations, stages);
        }
        return result;
    }

    private static bool IsLate(TurnItemDto turn, DashboardGoals goals) =>
        // Mismo redondeo que el Dashboard (EfDashboardOverviewReader), para que ambos digan lo mismo.
        turn.WaitingBusinessMs is { } ms && goals.ForAction(turn.Action) is { } goal && Math.Round(ms / BusinessDayMs, 1) > goal.Days;
}
