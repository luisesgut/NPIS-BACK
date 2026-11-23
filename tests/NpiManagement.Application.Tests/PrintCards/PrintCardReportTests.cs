using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Dashboard;
using NpiManagement.Application.PrintCards;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.PrintCards;

/// <summary>Módulo PrintCards (2026-11-23): historial, tiempos e indicadores por diseñador.</summary>
public class PrintCardReportTests
{
    private static readonly IBusinessCalendar Calendar = new MexicoBusinessCalendar();
    // Martes 10 y miércoles 11 de noviembre de 2026, hora de México (UTC-6).
    private static DateTimeOffset Mx(int day, int hour) => new(2026, 11, day, hour, 0, 0, TimeSpan.FromHours(-6));
    private static readonly DateTimeOffset Now = Mx(20, 12);

    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();

    private static PrintCardVersionSource Version(int n, DateTimeOffset at, Guid by, DateTimeOffset? rejectedAt = null, string? reason = null, params PrintCardApprovalSource[] approvals) =>
        new(n, $"PC-{n:000}", by, by == Ana ? "Ana" : "Beto", at, rejectedAt, reason, approvals);

    private static PrintCardNpiSource Npi(Guid designer, IReadOnlyList<PrintCardVersionSource> versions, IReadOnlyList<PrintCardEventSource>? events = null,
        IReadOnlyList<PrintCardTimerEventSource>? timer = null, string cliente = "MR. LUCKY", LifecycleStatus lifecycle = LifecycleStatus.Active) =>
        new(Guid.NewGuid(), "NPI-0080", cliente, "BOLSA", lifecycle, designer, versions, events ?? [], timer ?? []);

    [Fact]
    public void Active_time_counts_only_while_the_design_timer_ran_until_the_first_send()
    {
        // Corre 2 h el martes, se pausa (esperaba a Ingeniería) y corre 2 h más el miércoles antes del envío.
        var timer = new List<PrintCardTimerEventSource>
        {
            new(Mx(10, 10), TimerAction.Start), new(Mx(10, 12), TimerAction.Pause),
            new(Mx(11, 9), TimerAction.Resume), new(Mx(12, 9), TimerAction.Pause),
        };

        PrintCardMetrics.ActiveDesignMs(timer, Mx(11, 11), Calendar).Should().Be(4 * 3_600_000);
        PrintCardMetrics.ActiveDesignMs([], Mx(11, 11), Calendar).Should().BeNull();
    }

    [Fact]
    public void A_printcard_rejected_by_engineering_and_then_approved_by_the_client_has_its_whole_story()
    {
        var npi = Npi(Ana,
            [
                Version(1, Mx(10, 12), Ana, rejectedAt: Mx(10, 15), reason: "Falta la cara posterior"),
                Version(2, Mx(11, 10), Ana, null, null, new(ApproverRole.Engineering, Mx(11, 11)), new(ApproverRole.Client, Mx(13, 10))),
            ],
            [new(Mx(10, 9), "DesignAssigned", null), new(Mx(11, 12), "PrintCardSentToClient", null)],
            [new(Mx(10, 10), TimerAction.Start), new(Mx(10, 12), TimerAction.Pause), new(Mx(10, 15), TimerAction.Resume), new(Mx(11, 10), TimerAction.Pause)]);

        var r = PrintCardMetrics.Build(npi, new Dictionary<Guid, string> { [Ana] = "Ana López" }, Calendar, Now);

        r.Status.Should().Be("aprobado");
        r.DesignerName.Should().Be("Ana López");
        r.CurrentCode.Should().Be("PC-002");
        r.Versions.Should().Be(2);
        r.EngineeringRejections.Should().Be(1);
        r.FirstPass.Should().BeFalse();
        r.FirstSentAt.Should().Be(Mx(11, 12));
        r.ActiveHours.Should().Be(6, "2 h el martes antes de subir v1, 3 h del rechazo al cierre y 1 h el miércoles");
        r.CalendarDays.Should().Be(1.1);
        r.History.Select(h => h.Outcome).Should().Equal("rechazada_ing", "aprobada_cliente");
        r.History[0].RejectionReason.Should().Be("Falta la cara posterior");
        r.History[1].SentAt.Should().Be(Mx(11, 12));
    }

    [Fact]
    public void A_version_retired_by_the_system_is_not_counted_as_an_engineering_rejection()
    {
        var npi = Npi(Ana, [Version(1, Mx(10, 12), Ana, Mx(10, 14), NpiManagement.Domain.Npi.PrintCardRetiredByCommercialReason), Version(2, Mx(11, 10), Ana)]);

        var r = PrintCardMetrics.Build(npi, new Dictionary<Guid, string>(), Calendar, Now);

        r.EngineeringRejections.Should().Be(0);
        r.History[0].Outcome.Should().Be("retirada");
        r.History[0].RejectionReason.Should().BeNull();
        r.Status.Should().Be("en_proceso");
        r.DesignerName.Should().Be("Ana", "sin nombre en el directorio, se usa quien subió la versión");
    }

    [Fact]
    public void An_undone_send_does_not_count_and_client_changes_mark_the_printcard()
    {
        var undone = Npi(Ana, [Version(1, Mx(10, 12), Ana, null, null, new PrintCardApprovalSource(ApproverRole.Engineering, Mx(10, 13)))],
            [new(Mx(10, 14), "PrintCardSentToClient", null), new(Mx(10, 15), "ActionUndone", "PrintCardSentToClient")]);
        var changed = Npi(Ana, [Version(1, Mx(10, 12), Ana, null, null, new PrintCardApprovalSource(ApproverRole.Engineering, Mx(10, 13)))],
            [new(Mx(10, 14), "PrintCardSentToClient", null), new(Mx(11, 10), "ClientChangesRequested", "Cambiar el verde")]);

        var u = PrintCardMetrics.Build(undone, new Dictionary<Guid, string>(), Calendar, Now);
        u.FirstSentAt.Should().BeNull();
        u.Status.Should().Be("en_proceso");

        var c = PrintCardMetrics.Build(changed, new Dictionary<Guid, string>(), Calendar, Now);
        c.Status.Should().Be("cambios_cliente");
        c.ClientChangeRounds.Should().Be(1);
        c.FirstPass.Should().BeFalse("el cliente pidió cambios");
    }

    private sealed class FakeReader(params PrintCardNpiSource[] npis) : IPrintCardReportReader
    {
        public Task<IReadOnlyList<PrintCardNpiSource>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PrintCardNpiSource>>(npis);
    }

    private static PrintCardNpiSource Sent(Guid designer, DateTimeOffset sentAt, string cliente = "MR. LUCKY") =>
        Npi(designer, [Version(1, sentAt.AddHours(-3), designer, null, null, new PrintCardApprovalSource(ApproverRole.Engineering, sentAt.AddHours(-1)))],
            [new(sentAt.AddDays(-1), "DesignAssigned", null), new(sentAt, "PrintCardSentToClient", null)],
            [new(sentAt.AddHours(-5), TimerAction.Start), new(sentAt.AddHours(-3), TimerAction.Pause)], cliente);

    private static PrintCardReportQueryHandler Handler(params PrintCardNpiSource[] npis) =>
        new(new FakeReader(npis), new FakeUserDirectory(), Calendar, new TestClock(Now), new DashboardGoals());

    [Fact]
    public async Task Admins_see_every_designer_and_a_designer_only_sees_their_own()
    {
        var handler = Handler(Sent(Ana, Mx(10, 12)), Sent(Ana, Mx(11, 12), "DESTINY"), Sent(Beto, Mx(11, 15)),
            Npi(Beto, [Version(1, Mx(12, 10), Beto)]));

        var admin = await handler.HandleAsync(new PrintCardReportQuery(new CurrentUser(Guid.NewGuid(), ApplicationRoles.AdminDiseno, AreaType.Diseno), 6, null, null), CancellationToken.None);
        admin.OnlyMine.Should().BeFalse();
        admin.Sent.Current.Should().Be(3);
        admin.InProgress.Should().Be(1);
        admin.Records.Should().HaveCount(4);
        admin.Designers.Should().HaveCount(2);
        admin.Designers.Single(d => d.DesignerUserId == Ana).Sent.Should().Be(2);
        admin.Designers.Single(d => d.DesignerUserId == Beto).InProgress.Should().Be(1);
        admin.ByClient.First().Key.Should().Be("MR. LUCKY");
        admin.FirstPassPercent.Current.Should().Be(100);
        admin.GoalHours.Should().Be(27, "meta de 3 días hábiles para generar el PrintCard × 9 h");

        var designer = await handler.HandleAsync(new PrintCardReportQuery(new CurrentUser(Beto, ApplicationRoles.UsuarioDiseno, AreaType.Diseno), 6, null, Ana), CancellationToken.None);
        designer.OnlyMine.Should().BeTrue();
        designer.Records.Should().OnlyContain(r => r.DesignerUserId == Beto, "el filtro de otro diseñador se ignora");
        designer.Sent.Current.Should().Be(1);
    }

    [Fact]
    public async Task The_period_and_client_filters_apply_and_invalid_periods_are_rejected()
    {
        var handler = Handler(Sent(Ana, Mx(10, 12)), Sent(Ana, Now.AddMonths(-3)), Sent(Ana, Now.AddMonths(-5), "DESTINY"));
        var actor = new CurrentUser(Guid.NewGuid(), ApplicationRoles.AdminTotal, AreaType.Total);

        var month = await handler.HandleAsync(new PrintCardReportQuery(actor, 1, null, null), CancellationToken.None);
        month.Sent.Should().Be(new ComparedDto(1, 0));
        month.Records.Should().HaveCount(1);

        (await handler.HandleAsync(new PrintCardReportQuery(actor, 0, null, null), CancellationToken.None)).Sent.Current.Should().Be(3);
        (await handler.HandleAsync(new PrintCardReportQuery(actor, 0, "destiny", null), CancellationToken.None)).Records.Should().ContainSingle();

        var act = () => handler.HandleAsync(new PrintCardReportQuery(actor, 2, null, null), CancellationToken.None);
        await act.Should().ThrowAsync<NpiManagement.Application.Exceptions.RequestValidationException>();
    }
}
