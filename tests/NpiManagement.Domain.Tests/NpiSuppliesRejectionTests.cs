using FluentAssertions;
using NpiManagement.Domain.Exceptions;
using Xunit;
using static NpiManagement.Domain.Tests.NpiTestFactory;

namespace NpiManagement.Domain.Tests;

public class NpiSuppliesRejectionTests
{
    // 2026-11-25 (NPI-0095/0096): con el rechazo abierto el NPI queda detenido; Ingeniería lo puede retirar.
    [Fact]
    public void While_the_supplies_rejection_is_open_design_and_engineering_cannot_advance()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, "Falta película");

        Action[] blocked =
        [
            () => npi.ApproveDesignFeasibility(Designer, clock, null, null),
            () => npi.RejectArtwork(Designer, clock, Calendar, "Falta el logo", null),
            () => npi.ValidateArtwork(Designer, clock, Calendar),
            () => npi.RequestDesignChanges(Designer, clock, Calendar, "Corregir ancho", ["ancho"]),
            () => npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "¿Pantone?"),
            () => npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null),
            () => npi.RequestEngineeringChanges(Engineer, clock, Calendar, "Cambiar calibre", ["calibre"]),
        ];
        foreach (var act in blocked)
            act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("supplies-rejection-open");
    }

    [Fact]
    public void Engineering_can_withdraw_the_rejection_with_a_reason_and_resume_its_review()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, "Falta película");

        ((Action)(() => npi.WithdrawSuppliesRejection(Engineer, clock, Calendar, " ")))
            .Should().Throw<NpiValidationException>().Which.Code.Should().Be("supplies-withdraw-reason-required");

        npi.WithdrawSuppliesRejection(Engineer, clock, Calendar, "Ya llegaron los insumos");

        npi.SuppliesTemporaryRejection.Should().BeNull();
        npi.EngineeringAreaStatus.Should().NotBe(EngineeringAreaStatus.SuppliesTemporaryRejection);
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.CommercialTimer.IsRunning.Should().BeFalse("Comercial ya no tiene nada pendiente");
        npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);
        ((Action)(() => npi.WithdrawSuppliesRejection(Engineer, clock, Calendar, "otra vez")))
            .Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("no-open-supplies-rejection");
    }

    [Fact]
    public void The_agreement_keeps_commercial_running_if_it_still_has_new_artwork_to_upload()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RejectArtwork(Designer, clock, Calendar, "Falta el logo", null);
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, "Falta película");

        npi.ResolveSuppliesAgreement(Commercial, clock, Calendar, "El cliente acepta otra película", Guid.NewGuid());

        npi.CommercialTimer.IsRunning.Should().BeTrue("todavía le toca subir las artes nuevas");
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void RejectSuppliesTemporary_pauses_engineering_but_not_commercial_or_global()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, "Falta insumo X");

        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.SuppliesTemporaryRejection);
        npi.EngineeringTimer.IsRunning.Should().BeFalse();
        npi.GlobalTimer.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void RejectSuppliesTemporary_is_not_eligible_once_engineering_feasibility_is_already_approved()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.ApproveEngineeringFeasibility(Engineer, clock, Calendar, null);

        var act = () => npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("supplies-rejection-not-eligible");
    }

    [Fact]
    public void RejectSuppliesTemporary_twice_is_rejected()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        var act = () => npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("supplies-rejection-already-open");
    }

    [Fact]
    public void ResolveSuppliesAgreement_requires_an_open_temporary_rejection()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        var act = () => npi.ResolveSuppliesAgreement(Commercial, clock, Calendar, "Se resuelve así", Guid.NewGuid());

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("no-open-supplies-rejection");
    }

    [Fact]
    public void ResolveSuppliesAgreement_requires_text()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        var act = () => npi.ResolveSuppliesAgreement(Commercial, clock, Calendar, "   ", Guid.NewGuid());

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("supplies-agreement-text-required");
    }

    [Fact]
    public void ResolveSuppliesAgreement_resumes_engineering_and_clears_the_temporary_rejection()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        npi.ResolveSuppliesAgreement(Commercial, clock, Calendar, "Se compra a otro proveedor", Guid.NewGuid());

        npi.SuppliesTemporaryRejection.Should().BeNull();
        npi.SuppliesAgreement.Should().NotBeNull();
        npi.EngineeringTimer.IsRunning.Should().BeTrue();
        npi.EngineeringAreaStatus.Should().Be(EngineeringAreaStatus.Assigned);
    }

    [Fact]
    public void RejectSuppliesTotal_requires_an_open_temporary_rejection()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);

        var act = () => npi.RejectSuppliesTotal(Commercial, clock, Calendar);

        act.Should().Throw<InvalidNpiTransitionException>().Which.Code.Should().Be("no-open-supplies-rejection");
    }

    [Fact]
    public void RejectSuppliesTotal_is_terminal_and_closes_every_running_timer()
    {
        var clock = TestClock.MondayMorning();
        var npi = CreateActiveAssigned(clock);
        npi.RequestInformation(Designer, clock, Calendar, InformationRequestType.Commercial, "Pregunta"); // arranca CommercialTimer
        npi.RejectSuppliesTemporary(Engineer, clock, Calendar, null);

        npi.RejectSuppliesTotal(Commercial, clock, Calendar);

        npi.LifecycleStatus.Should().Be(LifecycleStatus.Rejected);
        npi.TerminalReason.Should().Be(TerminalReason.SuppliesRejectionTotal);
        npi.GlobalTimer.IsClosed.Should().BeTrue();
        npi.EngineeringTimer.IsClosed.Should().BeTrue();
        npi.CommercialTimer.IsClosed.Should().BeTrue();
    }
}
