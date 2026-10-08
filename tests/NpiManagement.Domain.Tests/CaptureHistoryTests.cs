using FluentAssertions;
using NpiManagement.Domain.ValueObjects;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

/// <summary>Bitácora de cambios de la captura: fase inicial al enviar y cada campo que cambia después.</summary>
public class CaptureHistoryTests
{
    private readonly TestClock _clock = TestClock.MondayMorning();

    [Fact]
    public void Draft_edits_are_not_logged_but_the_first_submission_takes_a_snapshot()
    {
        var npi = CreateDraft(_clock);

        npi.UpdateDraft(Commercial, _clock, npi.Capture with { Ancho = "100" }, npi.Priority).Should().BeEmpty();
        npi.Submit(Commercial, _clock, Calendar, NpiNumber.FromSequenceValue(1));

        CaptureDiff.FromSnapshot(npi.InitialCaptureJson)!.Ancho.Should().Be("100");
        npi.FirstSubmittedAt.Should().Be(_clock.UtcNow);
        npi.FirstSubmittedByUserId.Should().Be(Commercial.UserId);
    }

    [Fact]
    public void After_submission_every_changed_field_is_logged_with_who_and_when()
    {
        var npi = CreateActive(_clock);
        _clock.Advance(TimeSpan.FromHours(2));

        var changes = npi.UpdateDraft(Commercial, _clock, npi.Capture with { Ancho = "120", Alto = "30" }, Priority.Alta);

        changes.Select(c => (c.Field, c.After)).Should().BeEquivalentTo(new[] { ("ancho", "120"), ("alto", "30"), ("priority", "Alta") });
        changes.Should().OnlyContain(c => c.ChangedByUserId == Commercial.UserId && c.ChangedAt == _clock.UtcNow && c.ChangeRequestId == null);
        npi.AuditLog.Should().Contain(e => e.Action == "CaptureUpdated" && e.Details == "3 campos");
        CaptureDiff.FromSnapshot(npi.InitialCaptureJson)!.Ancho.Should().BeEmpty("la foto inicial no cambia");
    }

    [Fact]
    public void Saving_without_changes_logs_nothing()
    {
        var npi = CreateActive(_clock);

        npi.UpdateDraft(Commercial, _clock, npi.Capture, npi.Priority).Should().BeEmpty();
        npi.AuditLog.Should().NotContain(e => e.Action == "CaptureUpdated");
    }

    [Fact]
    public void Each_change_is_linked_to_the_request_that_asked_for_it()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "Corregir el ancho", ["ancho"]);
        _clock.Advance(TimeSpan.FromMinutes(5));
        npi.RequestDesignChanges(Designer, _clock, Calendar, "Corregir la tinta", ["tipoTinta"]);
        var engineeringRequest = npi.ChangeRequests.Single(c => c.FromArea == AreaType.Ingenieria);
        var designRequest = npi.ChangeRequests.Single(c => c.FromArea == AreaType.Diseno);

        var changes = npi.UpdateDraft(Commercial, _clock, npi.Capture with { Ancho = "120", TipoTinta = "FRENTE", Observaciones = "Aclarado" }, npi.Priority);

        changes.Single(c => c.Field == "ancho").Should().Match<Entities.CaptureChange>(c => c.ChangeRequestId == engineeringRequest.Id && c.WasRequested);
        changes.Single(c => c.Field == "tipoTinta").Should().Match<Entities.CaptureChange>(c => c.ChangeRequestId == designRequest.Id && c.WasRequested);
        // Nadie lo pidió, pero se cambió mientras se atendían solicitudes: se liga a la más reciente.
        changes.Single(c => c.Field == "observaciones").Should().Match<Entities.CaptureChange>(c => c.ChangeRequestId == designRequest.Id && !c.WasRequested);
    }

    [Fact]
    public void Once_the_request_is_resolved_later_edits_are_on_commercials_own_initiative()
    {
        var npi = CreateActiveAssigned(_clock);
        npi.RequestEngineeringChanges(Engineer, _clock, Calendar, "Corregir el ancho", ["ancho"]);
        _clock.Advance(TimeSpan.FromMinutes(5));
        npi.UpdateDraft(Commercial, _clock, npi.Capture with { Ancho = "120" }, npi.Priority);
        npi.Submit(Commercial, _clock, Calendar);

        var later = npi.UpdateDraft(Commercial, _clock, npi.Capture with { Ancho = "125" }, npi.Priority);

        later.Single().ChangeRequestId.Should().BeNull();
        later.Single().Before.Should().Be("120");
    }
}
