using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>Casos de autorización específicos de acciones de workflow que no quedan cubiertos por el camino feliz.</summary>
public class WorkflowAuthorizationEdgeCaseTests
{
    private static readonly Guid CommercialId = Guid.NewGuid();
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly CurrentUser Commercial = new(CommercialId, "UsuarioComercial", AreaType.Comercial);
    private static readonly CurrentUser Engineer = new(EngineerId, "UsuarioIngenieria", AreaType.Ingenieria);

    private static async Task<(FakeNpiRepository Repo, FakeUnitOfWork Uow, TestClock Clock, MexicoBusinessCalendar Calendar, Guid NpiId)> ActiveAssignedNpiAsync()
    {
        var repo = new FakeNpiRepository();
        var uow = new FakeUnitOfWork();
        var clock = TestClock.Default();
        var calendar = new MexicoBusinessCalendar();
        var numbers = new FakeNpiNumberGenerator();
        var notifications = new FakeNotificationService();
        var users = new FakeUserDirectory();

        var npiId = await new CreateNpiDraftCommandHandler(repo, uow, clock)
            .HandleAsync(new CreateNpiDraftCommand(CommercialId, "UsuarioComercial", "Comercial", new NpiCaptureDto(), Priority.Media, null), CancellationToken.None);
        await new SubmitNpiCommandHandler(repo, uow, clock, calendar, numbers, notifications, users)
            .HandleAsync(new SubmitNpiCommand(npiId, Commercial, "Comercial"), CancellationToken.None);
        await new AssignEngineeringCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new AssignEngineeringCommand(npiId, new CurrentUser(Guid.NewGuid(), "AdminIngenieria", AreaType.Ingenieria), "Admin", EngineerId), CancellationToken.None);

        return (repo, uow, clock, calendar, npiId);
    }

    [Fact]
    public async Task Supplies_temporary_then_total_rejection_reaches_Rejected()
    {
        var (repo, uow, clock, calendar, npiId) = await ActiveAssignedNpiAsync();
        var notifications = new FakeNotificationService();

        await new RejectSuppliesTemporaryCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new RejectSuppliesTemporaryCommand(npiId, Engineer, "Ingeniero", "Falta insumo X"), CancellationToken.None);

        var final = await new RejectSuppliesTotalCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new RejectSuppliesTotalCommand(npiId, Commercial, "Comercial"), CancellationToken.None);

        final.LifecycleStatus.Should().Be("Rejected");
        final.TerminalReason.Should().Be("SuppliesRejectionTotal");
    }

    [Fact]
    public async Task Only_AdminComercial_or_AdminTotal_can_cancel()
    {
        var (repo, uow, clock, calendar, npiId) = await ActiveAssignedNpiAsync();
        var notifications = new FakeNotificationService();
        var handler = new CancelNpiCommandHandler(repo, uow, clock, calendar, notifications);

        var act = async () => await handler.HandleAsync(new CancelNpiCommand(npiId, Commercial, "Comercial", "Ya no aplica"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>("UsuarioComercial no puede cancelar, sólo AdminComercial/AdminTotal");
    }

    [Fact]
    public async Task AdminComercial_can_cancel()
    {
        var (repo, uow, clock, calendar, npiId) = await ActiveAssignedNpiAsync();
        var notifications = new FakeNotificationService();
        var handler = new CancelNpiCommandHandler(repo, uow, clock, calendar, notifications);
        var admin = new CurrentUser(Guid.NewGuid(), "AdminComercial", AreaType.Comercial);

        var result = await handler.HandleAsync(new CancelNpiCommand(npiId, admin, "Admin", "Cliente canceló"), CancellationToken.None);

        result.LifecycleStatus.Should().Be("Cancelled");
    }

    [Fact]
    public async Task RejectByClient_requires_a_comercial_actor_even_though_it_represents_the_client_decision_8()
    {
        var (repo, uow, clock, calendar, npiId) = await ActiveAssignedNpiAsync();
        var handler = new RejectByClientCommandHandler(repo, uow, clock, calendar, new FakeNotificationService());

        // Un ingeniero (aunque asignado) no puede registrar la decisión del cliente.
        var act = async () => await handler.HandleAsync(new RejectByClientCommand(npiId, Engineer, "Ingeniero", "El cliente ya no quiere continuar", null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
