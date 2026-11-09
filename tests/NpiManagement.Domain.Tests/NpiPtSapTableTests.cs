using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Tabla copiada de Excel/SAP al cargar el PT (2026-11-09).</summary>
public class NpiPtSapTableTests
{
    [Fact]
    public void Guarda_la_tabla_pegada_con_el_PT()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);

        npi.RegisterSapPt(Engineer, clock, Calendar, "5001234", "BOLSA PILLOW", "Material\tDescripción\r\n5001234\tBOLSA PILLOW 45G\r\n");

        npi.PtSapTable.Should().Be("Material\tDescripción\r\n5001234\tBOLSA PILLOW 45G");
    }

    [Fact]
    public void Sin_tabla_queda_vacía_y_una_enorme_se_rechaza()
    {
        var clock = TestClock.MondayMorning();
        CreateWithPrintCardLoaded(clock).Also(n => n.RegisterSapPt(Engineer, clock, Calendar, "5001234", null)).PtSapTable.Should().BeNull();

        var npi = CreateWithPrintCardLoaded(clock);
        var act = () => npi.RegisterSapPt(Engineer, clock, Calendar, "5001234", null, new string('x', Npi.PtSapTableMaxLength + 1));
        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("pt-sap-table-too-long");
    }
}

internal static class NpiTestExtensions
{
    public static Npi Also(this Npi npi, Action<Npi> action) { action(npi); return npi; }
}
