using System.Linq.Expressions;
using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Dashboard;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Turns;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.Application.Tests;

/// <summary>Dashboard por rol (2026-11-09): el servidor decide qué vista ve cada quien.</summary>
public class DashboardOverviewTests
{
    private sealed class SpyReader : IDashboardOverviewReader
    {
        public DashboardView? View; public AreaType? Area;
        public Task<DashboardOverviewDto> GetAsync(DashboardView view, AreaType? area, Expression<Func<Npi, bool>> accessPredicate, DashboardFilter filter, DashboardGoals goals, CancellationToken cancellationToken)
        {
            View = view; Area = area;
            return Task.FromResult<DashboardOverviewDto>(null!);
        }
    }

    private static async Task<SpyReader> Ask(string role, AreaType area, AreaType? requested)
    {
        var spy = new SpyReader();
        await new DashboardOverviewQueryHandler(spy, new DashboardGoals())
            .HandleAsync(new DashboardOverviewQuery(new CurrentUser(Guid.NewGuid(), role, area), null, requested, new DashboardFilter(null, null, 1)), default);
        return spy;
    }

    [Theory]
    [InlineData("AdminTotal")]
    [InlineData("Director")]
    public async Task Direccion_ve_toda_la_operacion_y_puede_ver_un_area(string role)
    {
        (await Ask(role, AreaType.Total, null)).View.Should().Be(DashboardView.Director);
        var area = await Ask(role, AreaType.Total, AreaType.Diseno);
        area.View.Should().Be(DashboardView.Area);
        area.Area.Should().Be(AreaType.Diseno);
    }

    [Fact]
    public async Task El_admin_de_un_area_ve_su_area_aunque_pida_otra()
    {
        var spy = await Ask("AdminIngenieria", AreaType.Ingenieria, AreaType.Comercial);
        spy.View.Should().Be(DashboardView.Area);
        spy.Area.Should().Be(AreaType.Ingenieria);
    }

    [Fact]
    public async Task Un_usuario_ve_su_dashboard_personal()
    {
        var spy = await Ask("UsuarioDiseno", AreaType.Diseno, null);
        spy.View.Should().Be(DashboardView.Personal);
        spy.Area.Should().Be(AreaType.Diseno);
    }

    [Fact]
    public async Task Periodo_invalido_se_rechaza()
    {
        var act = () => new DashboardOverviewQueryHandler(new SpyReader(), new DashboardGoals())
            .HandleAsync(new DashboardOverviewQuery(new CurrentUser(Guid.NewGuid(), "AdminTotal", AreaType.Total), null, null, new DashboardFilter(null, null, 5)), default);
        await act.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public void Cada_accion_de_Tu_turno_tiene_su_etapa_y_meta()
    {
        var goals = new DashboardGoals();
        goals.ForAction(NpiTurns.ReviewPrintCard).Should().Be(("envio_cliente", 1.0));
        goals.ForAction(NpiTurns.RegisterClientResponse).Should().Be(("aprobacion_cliente", 3.0));
        goals.ForAction(NpiTurns.CompleteDraft).Should().BeNull("los borradores no tienen meta");
    }
}
