using FluentAssertions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>
/// Bug NPI-0037 (2026-11-13): Ingeniería pidió cambios (una pregunta), aprobó la factibilidad sin esperar
/// a Comercial y la solicitud quedó abierta para siempre: Diseño no podía generar el PrintCard
/// ("Comercial aún está corrigiendo datos del NPI") y nadie la veía.
/// </summary>
public class OrphanEngineeringRequestTests
{
    private readonly TestClock _clock = TestClock.MondayMorning();

    [Fact]
    public void Approving_feasibility_closes_the_engineering_request_still_open()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "¿Las dimensiones son las reales?", ["ancho"]);

        npi.ApproveEngineeringFeasibility(Engineer, _clock, Calendar, null);

        var request = npi.ChangeRequests.Single();
        request.IsResolved.Should().BeTrue();
        request.WithdrawnAt.Should().NotBeNull();
        request.ResponseComment.Should().Contain("aprobó la factibilidad");
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.FeasibilityApproved);
    }

    [Fact]
    public void After_closing_it_design_can_make_the_printcard()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "¿Las dimensiones son las reales?", ["ancho"]);
        npi.ApproveDesignFeasibility(Designer, _clock, null, ArtClassification.A);
        npi.ApproveEngineeringFeasibility(Engineer, _clock, Calendar, null);
        npi.ValidateArtwork(Designer, _clock);
        LinkQuote(npi, _clock);
        npi.AcceptSisproQuote(Commercial, _clock, Calendar, null, null);

        npi.CreatePrintCardVersion(Designer, _clock, Calendar, "PC-0001", File(npi, _clock), null);

        npi.CurrentPrintCardVersion.Should().NotBeNull();
    }

    [Fact]
    public void Approving_with_nothing_open_changes_nothing()
    {
        var npi = CreateActiveAssigned(_clock);

        npi.ApproveEngineeringFeasibility(Engineer, _clock, Calendar, null);

        npi.ChangeRequests.Should().BeEmpty();
        npi.AuditLog.Should().NotContain(e => e.Action == "ChangeRequestWithdrawn");
    }
}
