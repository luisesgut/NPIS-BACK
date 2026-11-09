using System.Linq.Expressions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Turns;
using NpiManagement.Domain;

namespace NpiManagement.Application.Dashboard;

/// <summary>
/// Metas en días hábiles (2026-11-09; propuesta aprobada por el dueño, ajustables en la sección
/// "Dashboard:Metas" del appsettings sin programar). Un NPI está "atrasado" cuando lo que le toca a un
/// área lleva más días hábiles que la meta de esa acción.
/// </summary>
public sealed class DashboardGoals
{
    public double CicloCompleto { get; set; } = 15;
    public double FactibilidadIngenieria { get; set; } = 2;
    public double FactibilidadDiseno { get; set; } = 2;
    public double GenerarPrintCard { get; set; } = 3;
    public double RevisionIngenieria { get; set; } = 1;
    public double EnvioCliente { get; set; } = 1;
    public double RespuestaCliente { get; set; } = 3;
    public double AltaSistemas { get; set; } = 2;
    public double Pedido { get; set; } = 2;
    public double EtSispro { get; set; } = 3;
    /// <summary>Correcciones y entregas de Comercial (cambios pedidos, artes, información, insumos).</summary>
    public double CorreccionesComercial { get; set; } = 2;
    /// <summary>Días hábiles promedio por NPI de cada área (vista "¿Quién necesita apoyo?").</summary>
    public double AreaComercial { get; set; } = 4;
    public double AreaIngenieria { get; set; } = 5;
    public double AreaDiseno { get; set; } = 6;

    /// <summary>Etapa de la ruta y meta (días) de cada acción de "Tu turno".</summary>
    public (string Stage, double Days)? ForAction(string action) => action switch
    {
        NpiTurns.AssignEngineer or NpiTurns.ReviewEngineeringFeasibility => ("ingenieria", FactibilidadIngenieria),
        NpiTurns.AssignDesigner or NpiTurns.ReviewDesignFeasibility or NpiTurns.ValidateArtwork => ("diseno", FactibilidadDiseno),
        NpiTurns.MakePrintCard or NpiTurns.FixPrintCard => ("envio_cliente", GenerarPrintCard),
        NpiTurns.ReviewPrintCard or NpiTurns.ReconfirmFeasibility or NpiTurns.AnswerDesignQuestions => ("envio_cliente", RevisionIngenieria),
        NpiTurns.SendPrintCardToClient => ("envio_cliente", EnvioCliente),
        NpiTurns.RegisterClientResponse => ("aprobacion_cliente", RespuestaCliente),
        NpiTurns.LoadPrintCard or NpiTurns.RegisterPt => ("alta_sistemas", AltaSistemas),
        NpiTurns.RegisterSapOrder => ("orden_venta", Pedido),
        NpiTurns.CreateEt or NpiTurns.LoadEtInSispro => ("et_sispro", EtSispro),
        NpiTurns.AttendEngineeringChanges or NpiTurns.AttendDesignChanges or NpiTurns.FinishCommercialEdit or NpiTurns.UploadNewArtwork
            or NpiTurns.AnswerInformationRequest or NpiTurns.ResolveSupplies or NpiTurns.QuoteInSispro => ("correcciones", CorreccionesComercial),
        _ => null, // Borradores: no tienen reloj ni meta.
    };
}

/// <summary>Qué ve cada quien: toda la operación, un área, o sólo lo suyo.</summary>
public enum DashboardView { Director, Area, Personal }

public sealed record DashboardFilter(string? Cliente, string? Priority, int Months);

public sealed record DashboardOverviewQuery(CurrentUser Actor, string? RequestedView, AreaType? RequestedArea, DashboardFilter Filter);

public sealed record PeriodDto(DateTimeOffset From, DateTimeOffset To, DateTimeOffset PreviousFrom);

/// <summary>Un número del periodo contra el periodo anterior (null si no hubo datos).</summary>
public sealed record ComparedDto(double? Current, double? Previous);

public sealed record StageLoadDto(string Stage, int Count, int Late);

/// <summary>Algo que le toca a un área en un NPI, con sus días y si ya pasó su meta.</summary>
public sealed record QueueItemDto(
    Guid NpiId, string? NpiNumber, string Cliente, string DescripcionProducto, string Area, string Action, string Stage,
    double? Days, double? GoalDays, bool Late, string? AssigneeName);

public sealed record MonthTrendDto(int Year, int Month, int Completed, int Lost, double? CycleDays);

public sealed record CountDto(string Key, int Count);

public sealed record AreaTimeDto(string Area, double? Days, double GoalDays, int Waiting);

public sealed record PersonLoadDto(Guid UserId, string Name, int Active, int Late);

public sealed record DashboardOverviewDto(
    string View,
    string? Area,
    PeriodDto Period,
    DashboardGoals Goals,
    int Active,
    ComparedDto Completed,
    ComparedDto CycleDays,
    ComparedDto OnTimePercent,
    int ToDo,
    int LateCount,
    IReadOnlyList<StageLoadDto> Stages,
    IReadOnlyList<QueueItemDto> Queue,
    IReadOnlyList<MonthTrendDto> Trend,
    IReadOnlyList<CountDto> Losses,
    IReadOnlyList<AreaTimeDto> AreaTimes,
    IReadOnlyList<PersonLoadDto> Load,
    /// <summary>Acciones del área en el periodo (bitácora): factibilidades, cambios pedidos, rechazos…</summary>
    IReadOnlyList<CountDto> Activity,
    /// <summary>Campos que más se pidió corregir a Comercial en el periodo.</summary>
    IReadOnlyList<CountDto> TopCorrectedFields);

public interface IDashboardOverviewReader
{
    Task<DashboardOverviewDto> GetAsync(
        DashboardView view, AreaType? area, Expression<Func<Npi, bool>> accessPredicate, DashboardFilter filter, DashboardGoals goals, CancellationToken cancellationToken);
}

/// <summary>
/// Dashboard por rol (2026-11-09): AdminTotal y Director ven toda la operación (y pueden ver la de un
/// área); el admin de un área, su área; un usuario normal, sólo lo suyo. Lo decide el servidor con el
/// rol del token, no la pantalla: pedir otra vista sin permiso cae en la que le toca.
/// </summary>
public sealed class DashboardOverviewQueryHandler(IDashboardOverviewReader reader, DashboardGoals goals)
{
    public Task<DashboardOverviewDto> HandleAsync(DashboardOverviewQuery query, CancellationToken cancellationToken)
    {
        var actor = query.Actor;
        if (query.Filter.Months is not (1 or 3 or 12))
            throw new RequestValidationException("dashboard-period", "El periodo debe ser 1, 3 o 12 meses.");

        var seesEverything = actor.Role == ApplicationRoles.AdminTotal || NpiAuthorization.IsDirector(actor);
        DashboardView view;
        AreaType? area;
        if (seesEverything)
        {
            area = query.RequestedArea is AreaType.Comercial or AreaType.Ingenieria or AreaType.Diseno ? query.RequestedArea : null;
            view = area is null ? DashboardView.Director : DashboardView.Area;
        }
        else if (ApplicationRoles.IsAdmin(actor.Role))
        {
            view = DashboardView.Area;
            area = actor.Area;
        }
        else
        {
            view = DashboardView.Personal;
            area = actor.Area;
        }

        return reader.GetAsync(view, area, NpiAuthorization.BuildAccessPredicate(actor), query.Filter, goals, cancellationToken);
    }
}
