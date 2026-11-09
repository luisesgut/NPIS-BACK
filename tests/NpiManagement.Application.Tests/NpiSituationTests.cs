using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Dashboard;
using NpiManagement.Application.Npis.Turns;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.Application.Tests;

/// <summary>Filtros "¿Qué necesita?" / "¿Dónde está?" de los paneles (2026-11-09).</summary>
public class NpiSituationTests
{
    private const long Day = 9L * 60 * 60 * 1000;
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    private static TurnItemDto Turn(Guid npi, AreaType area, string action, double days) =>
        new(npi, "NPI", "CLIENTE", "BOLSA", area.ToString(), action, DateTimeOffset.UtcNow, (long)(days * Day), null);

    private static readonly IReadOnlyList<TurnItemDto> Turns =
    [
        // A: Ingeniería revisa el PrintCard hace 3 días (meta 1) → atrasado, etapa PrintCard.
        Turn(A, AreaType.Ingenieria, NpiTurns.ReviewPrintCard, 3),
        // B: Comercial atiende cambios de Ingeniería y Diseño hace su factibilidad.
        Turn(B, AreaType.Comercial, NpiTurns.AttendEngineeringChanges, 0.5),
        Turn(B, AreaType.Diseno, NpiTurns.ReviewDesignFeasibility, 1),
        // C: borrador → no entra.
        Turn(C, AreaType.Comercial, NpiTurns.CompleteDraft, 10),
    ];

    private static IReadOnlyDictionary<Guid, NpiSituationFlags> For(string role, AreaType area, AreaType? panel = null) =>
        NpiSituationResolver.Resolve(Turns, new CurrentUser(Guid.NewGuid(), role, area), panel, new DashboardGoals());

    [Fact]
    public void Ingenieria_ve_lo_suyo_atrasado_y_lo_que_espera_a_otros()
    {
        var flags = For(ApplicationRoles.AdminIngenieria, AreaType.Ingenieria);

        flags.Keys.Should().BeEquivalentTo([A, B]);
        flags[A].Situations.Should().BeEquivalentTo([NpiSituation.Mine, NpiSituation.Late]);
        flags[A].Stages.Should().BeEquivalentTo([NpiStageGroup.PrintCard]);
        flags[B].Situations.Should().BeEquivalentTo([NpiSituation.Waiting, NpiSituation.Changes]);
        flags[B].Stages.Should().BeEquivalentTo([NpiStageGroup.Factibilidad]);
    }

    [Fact]
    public void AdminTotal_usa_el_area_del_panel()
    {
        For(ApplicationRoles.AdminTotal, AreaType.Total, AreaType.Comercial)[B].Situations.Should().Contain(NpiSituation.Mine);
        For(ApplicationRoles.AdminTotal, AreaType.Total, AreaType.Comercial)[A].Situations.Should().Contain(NpiSituation.Waiting);
    }

    [Fact]
    public void El_admin_de_Comercial_solo_cuenta_lo_suyo_como_te_toca()
    {
        var adminComercial = new CurrentUser(Guid.NewGuid(), ApplicationRoles.AdminComercial, AreaType.Comercial);
        TurnScope.IsRestricted(adminComercial).Should().BeTrue();
        TurnScope.IsRestricted(new CurrentUser(Guid.NewGuid(), ApplicationRoles.AdminIngenieria, AreaType.Ingenieria)).Should().BeFalse();

        // B le toca a Comercial pero no es suyo: no es "te toca" ni "esperando"; sí cuenta su etapa.
        var flags = NpiSituationResolver.Resolve(Turns, adminComercial, null, new DashboardGoals(), ownNpis: new HashSet<Guid>());
        flags[B].Situations.Should().NotContain([NpiSituation.Mine, NpiSituation.Waiting]);
        flags[B].Stages.Should().Contain(NpiStageGroup.Factibilidad);

        NpiSituationResolver.Resolve(Turns, adminComercial, null, new DashboardGoals(), ownNpis: new HashSet<Guid> { B })[B]
            .Situations.Should().Contain(NpiSituation.Mine);
    }

    [Fact]
    public void Un_usuario_normal_no_cuenta_asignar_como_suyo()
    {
        var turns = new[] { Turn(A, AreaType.Ingenieria, NpiTurns.AssignEngineer, 0.1) };
        NpiSituationResolver.Resolve(turns, new CurrentUser(Guid.NewGuid(), ApplicationRoles.UsuarioIngenieria, AreaType.Ingenieria), null, new DashboardGoals())[A]
            .Situations.Should().NotContain(NpiSituation.Mine);
    }
}
