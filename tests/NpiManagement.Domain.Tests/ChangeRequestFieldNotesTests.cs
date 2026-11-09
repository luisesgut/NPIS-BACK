using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Comentario por campo al pedir cambios (2026-11-03): Comercial lo ve junto a cada campo marcado.</summary>
public class ChangeRequestFieldNotesTests
{
    private readonly TestClock _clock = TestClock.MondayMorning();

    [Fact]
    public void Each_flagged_field_keeps_its_own_comment()
    {
        var npi = CreateActiveAssigned(_clock);

        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "Revisa estos datos", ["piCalibre", "cierreDimensiones"],
            new Dictionary<string, string> { ["piCalibre"] = " 150 micras son 600 gauges, ¿no será 37.5? ", ["cierreDimensiones"] = "   " });

        var notes = npi.ChangeRequests.Single().FieldNotes;
        notes.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("piCalibre", "150 micras son 600 gauges, ¿no será 37.5?"));
    }

    [Fact]
    public void A_comment_for_a_field_that_is_not_flagged_is_rejected()
    {
        var npi = CreateActiveAssigned(_clock);

        var act = () => npi.RequestDesignChanges(Designer, _clock, Calendar, "Revisa la tinta", ["tipoTinta"],
            new Dictionary<string, string> { ["ancho"] = "Esto no está marcado" });

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("field-note-not-flagged");
    }
}
