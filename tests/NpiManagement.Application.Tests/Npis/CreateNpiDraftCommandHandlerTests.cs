using FluentAssertions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public class CreateNpiDraftCommandHandlerTests
{
    [Fact]
    public async Task Creates_a_draft_and_persists_it_via_the_repository()
    {
        var repository = new FakeNpiRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateNpiDraftCommandHandler(repository, unitOfWork, TestClock.Default());
        var actorId = Guid.NewGuid();

        var command = new CreateNpiDraftCommand(
            actorId, "UsuarioComercial", "Comercial Uno",
            new NpiCaptureDto { Cliente = "Cliente X", NumeroCotizacion = "1001" },
            Priority.Alta, null);

        var id = await handler.HandleAsync(command, CancellationToken.None);

        var stored = await repository.GetByIdAsync(id, CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.LifecycleStatus.Should().Be(LifecycleStatus.Draft);
        stored.Capture.Cliente.Should().Be("Cliente X");
        stored.CreatedByUserId.Should().Be(actorId);
        unitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Preassigns_engineering_when_requested()
    {
        var repository = new FakeNpiRepository();
        var handler = new CreateNpiDraftCommandHandler(repository, new FakeUnitOfWork(), TestClock.Default());
        var engineerId = Guid.NewGuid();

        var command = new CreateNpiDraftCommand(
            Guid.NewGuid(), "UsuarioComercial", "Comercial Uno",
            new NpiCaptureDto(), Priority.Media, engineerId);

        var id = await handler.HandleAsync(command, CancellationToken.None);

        var stored = await repository.GetByIdAsync(id, CancellationToken.None);
        stored!.AssignedEngineeringUserId.Should().Be(engineerId);
    }
}
