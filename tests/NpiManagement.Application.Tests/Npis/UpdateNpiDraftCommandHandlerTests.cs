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

public class UpdateNpiDraftCommandHandlerTests
{
    private static (FakeNpiRepository Repo, Npi Npi, Guid CreatorId) SeedDraft(IClock clock)
    {
        var repo = new FakeNpiRepository();
        var creatorId = Guid.NewGuid();
        var actor = new Actor(creatorId, "UsuarioComercial", "Comercial Uno");
        var npi = Npi.CreateDraft(Guid.NewGuid(), actor, clock, new NpiCaptureDto { Cliente = "Original" }.ToDomain(), Priority.Media);
        repo.Add(npi);
        return (repo, npi, creatorId);
    }

    [Fact]
    public async Task Owner_can_update_the_draft_capture_data()
    {
        var clock = TestClock.Default();
        var (repo, npi, creatorId) = SeedDraft(clock);
        var handler = new UpdateNpiDraftCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new MexicoBusinessCalendar(), new FakeCaptureHistoryStore());
        var actor = new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial);

        var command = new UpdateNpiDraftCommand(npi.Id, actor, new NpiCaptureDto { Cliente = "Actualizado" }, Priority.Alta, repo.GetRowVersion(npi));
        var result = await handler.HandleAsync(command, CancellationToken.None);

        var reloaded = await repo.GetByIdAsync(npi.Id, CancellationToken.None);
        reloaded!.Capture.Cliente.Should().Be("ACTUALIZADO", "los clientes se guardan en mayúsculas");
        reloaded.Priority.Should().Be(Priority.Alta);
        result.RowVersion.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AdminTotal_can_update_any_draft()
    {
        var clock = TestClock.Default();
        var (repo, npi, _) = SeedDraft(clock);
        var handler = new UpdateNpiDraftCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new MexicoBusinessCalendar(), new FakeCaptureHistoryStore());
        var admin = new CurrentUser(Guid.NewGuid(), "AdminTotal", AreaType.Total);

        var command = new UpdateNpiDraftCommand(npi.Id, admin, new NpiCaptureDto { Cliente = "Editado por admin" }, Priority.Baja, repo.GetRowVersion(npi));
        var act = async () => await handler.HandleAsync(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task An_unrelated_commercial_user_gets_not_found_not_a_generic_forbidden()
    {
        var clock = TestClock.Default();
        var (repo, npi, _) = SeedDraft(clock);
        var handler = new UpdateNpiDraftCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new MexicoBusinessCalendar(), new FakeCaptureHistoryStore());
        var stranger = new CurrentUser(Guid.NewGuid(), "UsuarioComercial", AreaType.Comercial);

        var command = new UpdateNpiDraftCommand(npi.Id, stranger, new NpiCaptureDto(), Priority.Media, repo.GetRowVersion(npi));
        var act = async () => await handler.HandleAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>("no debe revelar que el NPI existe a quien no tiene acceso");
    }

    [Fact]
    public async Task An_engineering_user_cannot_edit_the_draft_even_if_assigned()
    {
        var clock = TestClock.Default();
        var (repo, npi, _) = SeedDraft(clock);
        npi.AssignEngineering(Guid.NewGuid(), new Actor(Guid.NewGuid(), "AdminIngenieria", "Admin"), clock);
        var handler = new UpdateNpiDraftCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new MexicoBusinessCalendar(), new FakeCaptureHistoryStore());
        var engineer = new CurrentUser(npi.AssignedEngineeringUserId!.Value, "UsuarioIngenieria", AreaType.Ingenieria);

        var command = new UpdateNpiDraftCommand(npi.Id, engineer, new NpiCaptureDto(), Priority.Media, repo.GetRowVersion(npi));
        var act = async () => await handler.HandleAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>("editar la captura es sólo de Comercial, aunque Ingeniería pueda ver el NPI");
    }

    [Fact]
    public async Task Updating_a_nonexistent_npi_throws_NotFoundException()
    {
        var repo = new FakeNpiRepository();
        var handler = new UpdateNpiDraftCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new MexicoBusinessCalendar(), new FakeCaptureHistoryStore());
        var actor = new CurrentUser(Guid.NewGuid(), "AdminTotal", AreaType.Total);

        var command = new UpdateNpiDraftCommand(Guid.NewGuid(), actor, new NpiCaptureDto(), Priority.Media, []);
        var act = async () => await handler.HandleAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
