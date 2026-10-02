using FluentAssertions;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Domain.Tests.ValueObjects;

public class TimerStateTests
{
    private readonly MexicoBusinessCalendar _calendar = new();

    [Fact]
    public void NotStarted_has_not_started_is_not_running_is_not_closed()
    {
        var t = TimerState.NotStarted;
        t.HasStarted.Should().BeFalse();
        t.IsRunning.Should().BeFalse();
        t.IsClosed.Should().BeFalse();
        t.ElapsedMs(TestClock.MondayMorning(), _calendar).Should().Be(0);
    }

    [Fact]
    public void StartOrResume_from_NotStarted_starts_and_runs()
    {
        var clock = TestClock.MondayMorning();
        var t = TimerState.NotStarted.StartOrResume(clock);

        t.HasStarted.Should().BeTrue();
        t.IsRunning.Should().BeTrue();
        t.StartAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void ElapsedMs_accrues_business_time_while_running()
    {
        var clock = TestClock.MondayMorning(); // lunes 10:00 local
        var t = TimerState.NotStarted.StartOrResume(clock);

        clock.Advance(TimeSpan.FromHours(2));
        t.ElapsedMs(clock, _calendar).Should().Be((long)TimeSpan.FromHours(2).TotalMilliseconds);
    }

    [Fact]
    public void Pause_freezes_elapsed_time_until_resumed()
    {
        var clock = TestClock.MondayMorning();
        var t = TimerState.NotStarted.StartOrResume(clock);
        clock.Advance(TimeSpan.FromHours(1));
        t = t.Pause(clock, _calendar);

        t.IsRunning.Should().BeFalse();
        var elapsedAtPause = t.ElapsedMs(clock, _calendar);
        elapsedAtPause.Should().Be((long)TimeSpan.FromHours(1).TotalMilliseconds);

        // El tiempo sigue avanzando en el reloj, pero el timer pausado no acumula más.
        clock.Advance(TimeSpan.FromHours(3));
        t.ElapsedMs(clock, _calendar).Should().Be(elapsedAtPause);
    }

    [Fact]
    public void Resume_after_pause_continues_accruing_from_the_accumulated_value()
    {
        var clock = TestClock.MondayMorning();
        var t = TimerState.NotStarted.StartOrResume(clock);
        clock.Advance(TimeSpan.FromHours(1));
        t = t.Pause(clock, _calendar);

        clock.Advance(TimeSpan.FromHours(5)); // tiempo "muerto" mientras está pausado
        t = t.StartOrResume(clock);
        clock.Advance(TimeSpan.FromHours(1));

        t.ElapsedMs(clock, _calendar).Should().Be((long)TimeSpan.FromHours(2).TotalMilliseconds);
    }

    [Fact]
    public void Close_stops_accrual_permanently_and_marks_as_closed()
    {
        var clock = TestClock.MondayMorning();
        var t = TimerState.NotStarted.StartOrResume(clock);
        clock.Advance(TimeSpan.FromHours(1));
        t = t.Close(clock, _calendar);

        t.IsClosed.Should().BeTrue();
        t.IsRunning.Should().BeFalse();
        var elapsedAtClose = t.ElapsedMs(clock, _calendar);

        clock.Advance(TimeSpan.FromDays(1));
        t.ElapsedMs(clock, _calendar).Should().Be(elapsedAtClose, "un timer cerrado nunca vuelve a acumular tiempo");
    }

    [Fact]
    public void StartOrResume_never_reopens_a_closed_timer()
    {
        var clock = TestClock.MondayMorning();
        var t = TimerState.NotStarted.StartOrResume(clock);
        t = t.Close(clock, _calendar);

        var reopened = t.StartOrResume(clock);

        reopened.IsClosed.Should().BeTrue();
        reopened.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void StartOrResume_on_an_already_running_timer_is_a_no_op()
    {
        var clock = TestClock.MondayMorning();
        var t = TimerState.NotStarted.StartOrResume(clock);
        var runningSinceBefore = t.RunningSinceAt;

        clock.Advance(TimeSpan.FromMinutes(30));
        var again = t.StartOrResume(clock);

        again.RunningSinceAt.Should().Be(runningSinceBefore, "reanudar un timer que ya corre no debe reiniciar su marca de tiempo");
    }

    [Fact]
    public void Pause_on_a_timer_that_never_started_is_a_no_op()
    {
        var clock = TestClock.MondayMorning();
        var t = TimerState.NotStarted.Pause(clock, _calendar);
        t.Should().Be(TimerState.NotStarted);
    }
}
