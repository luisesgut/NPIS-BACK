using FluentAssertions;
using NpiManagement.Domain.ValueObjects;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Clientes, productos y contactos en MAYÚSCULAS (2026-11-09).</summary>
public class NpiUppercaseNamesTests
{
    [Fact]
    public void Se_guardan_en_mayúsculas_con_acentos_y_Ñ_y_las_observaciones_no_se_tocan()
    {
        var clock = TestClock.MondayMorning();
        var capture = NpiCaptureData.Empty with
        {
            Cliente = "Panificación Ñandú", Descripcion = "Bolsa stand up café", ContactoCompras = "Ana López",
            Observaciones = "Ojo con el zipper",
        };

        var npi = Npi.CreateDraft(Guid.NewGuid(), Commercial, clock, capture, Priority.Media);

        npi.Capture.Cliente.Should().Be("PANIFICACIÓN ÑANDÚ");
        npi.Capture.Descripcion.Should().Be("BOLSA STAND UP CAFÉ");
        npi.Capture.ContactoCompras.Should().Be("ANA LÓPEZ");
        npi.Capture.Observaciones.Should().Be("Ojo con el zipper");
    }

    [Fact]
    public void Pasar_a_mayúsculas_no_cuenta_como_cambio_pero_otro_texto_sí()
    {
        var before = NpiCaptureData.Empty with { Cliente = "Mr Lucky", Observaciones = "hola" };

        CaptureDiff.Between(before, before with { Cliente = "MR LUCKY" }).Should().BeEmpty();
        CaptureDiff.Between(before, before with { Cliente = "MR. LUCKY" }).Should().ContainSingle(c => c.Field == "cliente");
        CaptureDiff.Between(before, before with { Observaciones = "HOLA" }).Should().ContainSingle(c => c.Field == "observaciones");
    }
}
