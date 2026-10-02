namespace NpiManagement.Domain.Common;

/// <summary>
/// Horario hábil Lunes–Viernes 09:00–18:00, America/Mexico_City (offset fijo UTC-6,
/// sin horario de verano desde 2022), excluyendo festivos federales MX (Art. 74 LFT).
/// Puerto directo de la lógica de src/lib/businessHours.ts del frontend (ver docs/backend-analysis.md §6).
/// Los festivos están hardcodeados 2024-2030 igual que en el frontend; si el negocio necesita
/// administrarlos sin desplegar código, se puede reemplazar esta implementación por una respaldada
/// en una tabla `PublicHoliday` sin cambiar la interfaz `IBusinessCalendar` (ver ADR-018).
/// </summary>
public sealed class MexicoBusinessCalendar : IBusinessCalendar
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(-6);
    private const int StartHour = 9;
    private const int EndHour = 18;
    private const int MaxDaysScanned = 4000; // salvaguarda, igual que en el frontend

    private static readonly HashSet<DateOnly> Holidays =
    [
        // 2024
        new(2024, 1, 1), new(2024, 2, 5), new(2024, 3, 18), new(2024, 5, 1),
        new(2024, 9, 16), new(2024, 10, 1), new(2024, 11, 18), new(2024, 12, 25),
        // 2025
        new(2025, 1, 1), new(2025, 2, 3), new(2025, 3, 17), new(2025, 5, 1),
        new(2025, 9, 16), new(2025, 11, 17), new(2025, 12, 25),
        // 2026
        new(2026, 1, 1), new(2026, 2, 2), new(2026, 3, 16), new(2026, 5, 1),
        new(2026, 9, 16), new(2026, 11, 16), new(2026, 12, 25),
        // 2027
        new(2027, 1, 1), new(2027, 2, 1), new(2027, 3, 15), new(2027, 5, 1),
        new(2027, 9, 16), new(2027, 11, 15), new(2027, 12, 25),
        // 2028
        new(2028, 1, 1), new(2028, 2, 7), new(2028, 3, 20), new(2028, 5, 1),
        new(2028, 9, 16), new(2028, 11, 20), new(2028, 12, 1), new(2028, 12, 25),
        // 2029
        new(2029, 1, 1), new(2029, 2, 5), new(2029, 3, 19), new(2029, 5, 1),
        new(2029, 9, 16), new(2029, 11, 19), new(2029, 12, 25),
        // 2030
        new(2030, 1, 1), new(2030, 2, 4), new(2030, 3, 18), new(2030, 5, 1),
        new(2030, 9, 16), new(2030, 11, 18), new(2030, 12, 25),
    ];

    public bool IsBusinessDay(DateOnly date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        return !Holidays.Contains(date);
    }

    public long BusinessMillisecondsBetween(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start) return 0;

        var startLocal = start.ToOffset(Offset);
        var endLocal = end.ToOffset(Offset);

        long totalMs = 0;
        var cursor = startLocal;

        for (var i = 0; i < MaxDaysScanned && cursor < endLocal; i++)
        {
            var day = DateOnly.FromDateTime(cursor.DateTime);
            var dayStart = new DateTimeOffset(day.Year, day.Month, day.Day, StartHour, 0, 0, Offset);
            var dayEnd = new DateTimeOffset(day.Year, day.Month, day.Day, EndHour, 0, 0, Offset);
            var nextDayMidnight = new DateTimeOffset(day.Year, day.Month, day.Day, 0, 0, 0, Offset).AddDays(1);

            if (IsBusinessDay(day))
            {
                var windowStart = cursor > dayStart ? cursor : dayStart;
                var windowEnd = endLocal < dayEnd ? endLocal : dayEnd;
                if (windowEnd > windowStart)
                    totalMs += (long)(windowEnd - windowStart).TotalMilliseconds;
            }

            if (nextDayMidnight <= cursor) break; // salvaguarda de seguridad
            cursor = nextDayMidnight < endLocal ? nextDayMidnight : endLocal;
        }

        return totalMs;
    }
}
