using FluentAssertions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>
/// Cuando Ingeniería pide cambios y Comercial reenvía, debe quedar claro qué se corrigió:
/// qué campos cambiaron (antes → después), quién lo hizo y su comentario.
/// </summary>
public class ChangeRequestResolutionTests
{
    [Fact]
    public void Resubmitting_records_which_fields_commercial_changed_and_its_comment()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RequestEngineeringChanges(Engineer, clock, Calendar, "El ancho no coincide con el plano", ["ancho"]);

        npi.UpdateDraft(npi.Capture with { Ancho = "304.8", Observaciones = "Confirmado con el cliente" }, npi.Priority);
        npi.Submit(Commercial, clock, Calendar, comment: "Corregí el ancho según el plano v2");

        var cr = npi.ChangeRequests.Single();
        cr.IsResolved.Should().BeTrue();
        cr.ResolvedByName.Should().Be(Commercial.Name);
        cr.ResponseComment.Should().Be("Corregí el ancho según el plano v2");
        cr.ChangesApplied.Should().BeEquivalentTo(new[]
        {
            new Entities.FieldChange("ancho", "", "304.8"),
            new Entities.FieldChange("observaciones", "", "Confirmado con el cliente"),
        });
    }

    [Fact]
    public void Resubmitting_without_changes_is_recorded_as_no_changes()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RequestEngineeringChanges(Engineer, clock, Calendar, "Revisar", []);

        npi.Submit(Commercial, clock, Calendar);

        var cr = npi.ChangeRequests.Single();
        cr.IsResolved.Should().BeTrue();
        cr.ChangesApplied.Should().BeEmpty();
        cr.ResponseComment.Should().BeNull();
    }

    [Fact]
    public void Each_round_trip_is_resolved_separately()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.RequestEngineeringChanges(Engineer, clock, Calendar, "Ronda 1", ["ancho"]);
        npi.UpdateDraft(npi.Capture with { Ancho = "100" }, npi.Priority);
        npi.Submit(Commercial, clock, Calendar);
        clock.Advance(TimeSpan.FromMinutes(5));
        npi.RequestEngineeringChanges(Engineer, clock, Calendar, "Ronda 2", ["alto"]);
        npi.UpdateDraft(npi.Capture with { Alto = "200" }, npi.Priority);
        npi.Submit(Commercial, clock, Calendar);

        var rounds = npi.ChangeRequests.OrderBy(c => c.RequestedAt).ToList();
        rounds[0].ChangesApplied.Select(c => c.Field).Should().Equal("ancho");
        rounds[1].ChangesApplied.Select(c => c.Field).Should().Equal("alto");
    }
}
