using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class NpiCommentsAttachmentsAndCancelTests
{
    [Fact]
    public void AddComment_rejects_empty_text()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);

        var act = () => npi.AddComment(Commercial, clock, "   ");

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("comment-empty");
    }

    [Fact]
    public void AddComment_is_allowed_even_when_the_npi_is_terminal()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);
        npi.RejectNoFeasibility(Engineer, clock, Calendar, null);

        var act = () => npi.AddComment(Commercial, clock, "Nota histórica tras el cierre");

        act.Should().NotThrow();
        npi.Comments.Should().ContainSingle();
    }

    [Fact]
    public void RegisterAttachment_increments_version_per_context()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);

        var first = npi.RegisterAttachment(Commercial, clock, "a.pdf", "application/pdf", 100, "key-1", null, AttachmentContext.Comment);
        var second = npi.RegisterAttachment(Commercial, clock, "b.pdf", "application/pdf", 200, "key-2", null, AttachmentContext.Comment);
        var otherContext = npi.RegisterAttachment(Commercial, clock, "c.pdf", "application/pdf", 300, "key-3", null, AttachmentContext.Feasibility);

        first.Version.Should().Be(1);
        second.Version.Should().Be(2);
        otherContext.Version.Should().Be(1, "el contador de versión es independiente por contexto");
    }

    [Fact]
    public void Cancel_requires_a_reason()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);

        var act = () => npi.Cancel(Commercial, clock, Calendar, "  ");

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("cancel-reason-required");
    }

    [Fact]
    public void Cancel_from_draft_sets_cancelled_status()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateDraft(clock);

        npi.Cancel(Commercial, clock, Calendar, "Ya no se requiere");

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Cancelled);
    }

    [Fact]
    public void Cancel_from_active_closes_any_open_timers()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.Cancel(Commercial, clock, Calendar, "Cliente canceló el proyecto");

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Cancelled);
        npi.GlobalTimer.IsClosed.Should().BeTrue();
        npi.EngineeringTimer.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Cancel_twice_is_rejected_because_the_npi_is_already_terminal()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActive(clock);
        npi.Cancel(Commercial, clock, Calendar, "Motivo inicial");

        var act = () => npi.Cancel(Commercial, clock, Calendar, "Otro motivo");

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("npi-terminal");
    }

    [Fact]
    public void DomainEvents_accumulate_and_can_be_cleared()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateDraft(clock);
        npi.Submit(Commercial, clock, Calendar, NpiManagement.Domain.ValueObjects.NpiNumber.FromSequenceValue(1));

        npi.DomainEvents.Should().ContainSingle(e => e is NpiManagement.Domain.Events.NpiSubmittedEvent);

        npi.ClearDomainEvents();

        npi.DomainEvents.Should().BeEmpty();
    }
}
