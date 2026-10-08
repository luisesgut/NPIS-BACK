using FluentAssertions;
using NpiManagement.Application.Npis;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>Lo que el front necesita para que el cronómetro se vea igual en todas las pantallas y computadoras.</summary>
public class TimerDtoTests
{
    private readonly IBusinessCalendar _calendar = new MexicoBusinessCalendar();
    private readonly TestClock _clock = TestClock.Default(); // lunes 5-ene-2026, 10:00 hora de México

    [Fact]
    public void In_business_hours_a_running_timer_ticks_until_the_end_of_the_day()
    {
        var timer = TimerState.NotStarted.StartOrResume(_clock);
        _clock.Advance(TimeSpan.FromHours(1));

        var dto = NpiMapper.ToTimerDto(timer, _clock, _calendar);

        dto.IsRunning.Should().BeTrue();
        dto.IsTicking.Should().BeTrue();
        dto.ElapsedMs.Should().Be((long)TimeSpan.FromHours(1).TotalMilliseconds);
        dto.MsUntilChange.Should().Be((long)TimeSpan.FromHours(7).TotalMilliseconds, "de las 11:00 al cierre de las 18:00");
    }

    [Fact]
    public void After_hours_a_running_timer_does_not_tick_until_the_next_morning()
    {
        var timer = TimerState.NotStarted.StartOrResume(_clock);
        _clock.Advance(TimeSpan.FromHours(10)); // 20:00

        var dto = NpiMapper.ToTimerDto(timer, _clock, _calendar);

        dto.IsRunning.Should().BeTrue();
        dto.IsTicking.Should().BeFalse("fuera de horario el servidor no cuenta, así que el front tampoco debe sumar");
        dto.ElapsedMs.Should().Be((long)TimeSpan.FromHours(8).TotalMilliseconds);
        dto.MsUntilChange.Should().Be((long)TimeSpan.FromHours(13).TotalMilliseconds, "abre mañana a las 9:00");
    }

    [Fact]
    public void A_paused_timer_never_ticks()
    {
        var timer = TimerState.NotStarted.StartOrResume(_clock);
        _clock.Advance(TimeSpan.FromHours(1));
        timer = timer.Pause(_clock, _calendar);

        var dto = NpiMapper.ToTimerDto(timer, _clock, _calendar);

        dto.IsTicking.Should().BeFalse();
        dto.MsUntilChange.Should().BeNull();
    }
}
