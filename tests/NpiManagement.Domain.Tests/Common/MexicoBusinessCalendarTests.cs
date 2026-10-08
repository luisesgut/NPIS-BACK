using FluentAssertions;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Domain.Tests.Common;

public class MexicoBusinessCalendarTests
{
    private readonly MexicoBusinessCalendar _calendar = new();

    private static DateTimeOffset MxLocal(int y, int m, int d, int h, int min = 0) => new(y, m, d, h, min, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void IsBusinessDay_returns_false_for_weekends()
    {
        _calendar.IsBusinessDay(new DateOnly(2026, 1, 3)).Should().BeFalse(); // sábado
        _calendar.IsBusinessDay(new DateOnly(2026, 1, 4)).Should().BeFalse(); // domingo
    }

    [Fact]
    public void IsBusinessDay_returns_false_for_MX_holidays()
    {
        _calendar.IsBusinessDay(new DateOnly(2026, 1, 1)).Should().BeFalse(); // Año Nuevo
        _calendar.IsBusinessDay(new DateOnly(2026, 12, 25)).Should().BeFalse(); // Navidad
    }

    [Fact]
    public void IsBusinessDay_returns_true_for_a_regular_weekday()
    {
        _calendar.IsBusinessDay(new DateOnly(2026, 1, 5)).Should().BeTrue(); // lunes
    }

    [Fact]
    public void BusinessMillisecondsBetween_returns_zero_when_end_is_not_after_start()
    {
        var t = MxLocal(2026, 1, 5, 10);
        _calendar.BusinessMillisecondsBetween(t, t).Should().Be(0);
        _calendar.BusinessMillisecondsBetween(t, t.AddHours(-1)).Should().Be(0);
    }

    [Fact]
    public void BusinessMillisecondsBetween_counts_full_window_within_same_business_day()
    {
        var start = MxLocal(2026, 1, 5, 10); // lunes 10:00
        var end = MxLocal(2026, 1, 5, 12);   // lunes 12:00
        _calendar.BusinessMillisecondsBetween(start, end).Should().Be((long)TimeSpan.FromHours(2).TotalMilliseconds);
    }

    [Fact]
    public void BusinessMillisecondsBetween_clips_to_the_09_18_window()
    {
        var start = MxLocal(2026, 1, 5, 6);  // antes de abrir
        var end = MxLocal(2026, 1, 5, 22);   // después de cerrar
        _calendar.BusinessMillisecondsBetween(start, end).Should().Be((long)TimeSpan.FromHours(9).TotalMilliseconds); // 09:00-18:00
    }

    [Fact]
    public void BusinessMillisecondsBetween_excludes_the_weekend()
    {
        var friday1700 = MxLocal(2026, 1, 2, 17);  // viernes 17:00
        var monday1000 = MxLocal(2026, 1, 5, 10);  // lunes 10:00
        // viernes: 17:00-18:00 = 1h; sábado/domingo excluidos; lunes: 09:00-10:00 = 1h
        _calendar.BusinessMillisecondsBetween(friday1700, monday1000).Should().Be((long)TimeSpan.FromHours(2).TotalMilliseconds);
    }

    [Fact]
    public void BusinessMillisecondsBetween_excludes_a_holiday_in_the_middle()
    {
        // 2026-05-01 es festivo (Día del Trabajo, viernes).
        var thursday1700 = MxLocal(2026, 4, 30, 17); // jueves 17:00
        var monday0930 = MxLocal(2026, 5, 4, 9, 30);  // lunes 09:30 (2026-05-01 es festivo)
        // jueves: 1h; viernes 1-may excluido; sábado/domingo excluidos; lunes: 09:00-09:30 = 0.5h
        _calendar.BusinessMillisecondsBetween(thursday1700, monday0930).Should().Be((long)TimeSpan.FromHours(1.5).TotalMilliseconds);
    }

    [Fact]
    public void BusinessMillisecondsBetween_spanning_several_full_business_days()
    {
        var mondayStart = MxLocal(2026, 1, 5, 9);  // lunes 09:00
        var wednesdayEnd = MxLocal(2026, 1, 7, 18); // miércoles 18:00
        // lunes + martes + miércoles completos = 3 * 9h
        _calendar.BusinessMillisecondsBetween(mondayStart, wednesdayEnd).Should().Be((long)TimeSpan.FromHours(27).TotalMilliseconds);
    }

    [Fact]
    public void IsBusinessTime_only_inside_the_working_window_of_a_business_day()
    {
        _calendar.IsBusinessTime(MxLocal(2026, 10, 8, 10)).Should().BeTrue();   // jueves
        _calendar.IsBusinessTime(MxLocal(2026, 10, 8, 8, 59)).Should().BeFalse();
        _calendar.IsBusinessTime(MxLocal(2026, 10, 8, 18)).Should().BeFalse();
        _calendar.IsBusinessTime(MxLocal(2026, 10, 10, 11)).Should().BeFalse(); // sábado
    }

    [Theory]
    [InlineData(2026, 10, 8, 10, 2026, 10, 8, 18)]   // en horario: cierra hoy a las 18
    [InlineData(2026, 10, 8, 7, 2026, 10, 8, 9)]     // antes de abrir: abre hoy a las 9
    [InlineData(2026, 10, 9, 19, 2026, 10, 12, 9)]   // viernes en la noche: lunes a las 9
    [InlineData(2026, 11, 13, 19, 2026, 11, 17, 9)]  // puente: el lunes 16 es festivo
    public void NextBoundary_is_the_next_open_or_close(int y, int m, int d, int h, int ey, int em, int ed, int eh)
    {
        _calendar.NextBoundary(MxLocal(y, m, d, h)).Should().Be(MxLocal(ey, em, ed, eh));
    }
}
