using NpiManagement.Domain.Common;

namespace NpiManagement.Domain;

/// <summary>
/// "Ponle orden de venta" (2026-11-27, propuesta para que Comercial cierre sus flujos): cuando Ingeniería carga
/// el PT, Comercial tiene <see cref="GraceBusinessDays"/> días hábiles para registrar la orden de venta. Después,
/// no puede crear NPIs nuevos hasta registrarla o indicar que el cliente aún no hace pedido (con fecha para
/// volver a avisar, máx. <see cref="MaxDeferralDays"/> días). Fechas en hora de México (UTC-6 fijo, igual que
/// MexicoBusinessCalendar).
/// </summary>
public static class SapOrderHold
{
    public const int GraceBusinessDays = 3;
    public const int MaxDeferralDays = 30;
    public const int DeferralReasonMaxLength = 1000;

    private static readonly TimeSpan MexicoOffset = TimeSpan.FromHours(-6);

    public static DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(MexicoOffset).DateTime);

    /// <summary>Día desde el que bloquea: el día del PT más 3 días hábiles. Sin fecha del PT (datos viejos), desde ya.</summary>
    public static DateOnly BlocksFrom(DateTimeOffset? ptRegisteredAt, IBusinessCalendar calendar)
    {
        if (ptRegisteredAt is not { } at) return DateOnly.MinValue;
        var day = LocalDate(at);
        for (var counted = 0; counted < GraceBusinessDays;)
        {
            day = day.AddDays(1);
            if (calendar.IsBusinessDay(day)) counted++;
        }
        return day;
    }

    /// <summary>Ya pasó la gracia y no está pospuesto (o lo pospuesto ya venció).</summary>
    public static bool IsBlocking(DateTimeOffset? ptRegisteredAt, DateOnly? deferredUntil, DateOnly today, IBusinessCalendar calendar) =>
        today >= BlocksFrom(ptRegisteredAt, calendar) && !(deferredUntil is { } until && today < until);
}
