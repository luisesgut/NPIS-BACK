using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>"Ponle orden de venta antes de crear otro NPI" (2026-11-27): gracia de 3 días hábiles y "el cliente aún no hace pedido".</summary>
public class NpiSapOrderHoldTests
{
    private static DateTimeOffset MxAt(int y, int m, int d, int hour = 10) => new(y, m, d, hour, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void Bloquea_a_los_3_dias_habiles_del_PT_sin_contar_fines_de_semana_ni_festivos()
    {
        // Lunes 5 → jueves 8 de enero de 2026.
        SapOrderHold.BlocksFrom(MxAt(2026, 1, 5), Calendar).Should().Be(new DateOnly(2026, 1, 8));
        // Viernes 9 → miércoles 14 (se brinca el fin de semana).
        SapOrderHold.BlocksFrom(MxAt(2026, 1, 9), Calendar).Should().Be(new DateOnly(2026, 1, 14));
        // Viernes 30 de enero → jueves 5 de febrero (el lunes 2 es festivo).
        SapOrderHold.BlocksFrom(MxAt(2026, 1, 30), Calendar).Should().Be(new DateOnly(2026, 2, 5));
        // Un PT cargado de noche en México sigue contando desde ese día (no desde el día UTC).
        SapOrderHold.BlocksFrom(MxAt(2026, 1, 5, 22), Calendar).Should().Be(new DateOnly(2026, 1, 8));
    }

    [Fact]
    public void Pospuesto_no_bloquea_hasta_la_fecha_y_al_llegar_vuelve_a_bloquear()
    {
        var pt = MxAt(2026, 1, 5);
        SapOrderHold.IsBlocking(pt, null, new DateOnly(2026, 1, 7), Calendar).Should().BeFalse("aún está en gracia");
        SapOrderHold.IsBlocking(pt, null, new DateOnly(2026, 1, 8), Calendar).Should().BeTrue();
        SapOrderHold.IsBlocking(pt, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 9), Calendar).Should().BeFalse();
        SapOrderHold.IsBlocking(pt, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), Calendar).Should().BeTrue();
        SapOrderHold.IsBlocking(null, null, new DateOnly(2026, 1, 5), Calendar).Should().BeTrue("sin fecha del PT, bloquea desde ya");
    }

    [Fact]
    public void Comercial_indica_que_el_cliente_aun_no_hace_pedido()
    {
        var clock = TestClock.MondayMorning(); // lunes 5 de enero de 2026
        var npi = CreateWithPrintCardLoaded(clock);
        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", null);

        npi.DeferSapOrder(Commercial, clock, new DateOnly(2026, 1, 20), "  El cliente confirma pedido después de su inventario  ");

        npi.SapOrderDeferredUntil.Should().Be(new DateOnly(2026, 1, 20));
        npi.SapOrderDeferralReason.Should().Be("El cliente confirma pedido después de su inventario");
        npi.SapOrderDeferredByName.Should().Be(Commercial.Name);
        npi.AuditLog.Last().Action.Should().Be("SapOrderDeferred");

        // Se puede volver a posponer, y la orden se registra igual cuando llegue.
        npi.DeferSapOrder(Commercial, clock, new DateOnly(2026, 1, 30), "Otra semana");
        npi.SapOrderDeferredUntil.Should().Be(new DateOnly(2026, 1, 30));
        npi.RegisterSapOrder(Commercial, clock, Calendar, "125530");
        npi.PedidoSapNumber.Should().Be("125530");
    }

    [Theory]
    [InlineData(2026, 1, 5, "Motivo", "sap-order-deferral-date-invalid")]   // hoy
    [InlineData(2026, 2, 5, "Motivo", "sap-order-deferral-too-far")]        // más de 30 días
    [InlineData(2026, 1, 20, "   ", "sap-order-deferral-reason-required")]
    public void Posponer_valida_fecha_y_motivo(int y, int m, int d, string reason, string code)
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);
        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", null);

        var act = () => npi.DeferSapOrder(Commercial, clock, new DateOnly(y, m, d), reason);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void No_se_pospone_sin_PT_ni_con_el_pedido_ya_registrado()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);

        var sinPt = () => npi.DeferSapOrder(Commercial, clock, new DateOnly(2026, 1, 20), "Motivo");
        sinPt.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("pt-not-registered");

        npi.RegisterSapPt(Engineer, clock, Calendar, "PT-001", null);
        npi.RegisterSapOrder(Commercial, clock, Calendar, "125530");
        var conPedido = () => npi.DeferSapOrder(Commercial, clock, new DateOnly(2026, 1, 20), "Motivo");
        conPedido.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("pedido-sap-already-registered");
    }
}
