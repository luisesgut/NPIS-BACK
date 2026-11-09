using FluentAssertions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>El motivo del cierre queda a la vista (bug NPI-0034/0035: Comercial veía "No factible" sin el porqué).</summary>
public class NpiClosingInfoTests
{
    [Fact]
    public void Rechazo_por_no_factibilidad_expone_el_motivo_quien_y_cuando()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.RejectNoFeasibility(Engineer, clock, Calendar, "El calibre no se puede laminar");

        npi.ClosingEvent.Should().NotBeNull();
        npi.ClosingEvent!.Details.Should().Be("El calibre no se puede laminar");
        npi.ClosingEvent.ActorRole.Should().Be(Engineer.Role);
    }

    [Fact]
    public void Cancelacion_expone_el_motivo()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.Cancel(Commercial, clock, Calendar, "El cliente ya no lo quiere");

        npi.ClosingEvent!.Details.Should().Be("El cliente ya no lo quiere");
    }

    [Fact]
    public void Un_npi_abierto_no_tiene_cierre()
    {
        var clock = TestClock.MondayMorning();
        CreateActiveAssigned(clock).ClosingEvent.Should().BeNull();
    }
}
