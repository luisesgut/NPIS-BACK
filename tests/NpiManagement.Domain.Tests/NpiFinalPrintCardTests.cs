using FluentAssertions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Cuál es la versión final del PrintCard (2026-11-09): la que se cargó en sistema.</summary>
public class NpiFinalPrintCardTests
{
    [Fact]
    public void Mientras_no_se_carga_no_hay_versión_final()
    {
        var clock = TestClock.MondayMorning();
        CreateWithFullyApprovedPrintCard(clock).FinalPrintCardVersion.Should().BeNull("todavía no la carga Diseño en sistema");
    }

    [Fact]
    public void La_cargada_en_sistema_es_la_final()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateWithPrintCardLoaded(clock);

        npi.FinalPrintCardVersion.Should().NotBeNull();
        npi.FinalPrintCardVersion!.Id.Should().Be(npi.PrintCardLoadedVersionId!.Value);
    }
}
