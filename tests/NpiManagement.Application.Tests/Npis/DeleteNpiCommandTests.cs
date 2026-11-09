using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Exceptions;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>Eliminar un NPI (2026-11-03): sólo AdminTotal y escribiendo el folio para confirmar.</summary>
public sealed class DeleteNpiCommandTests
{
    private sealed class RecordingDeletion : INpiDeletion
    {
        public List<(Guid Id, string Label)> Deleted { get; } = [];
        public Task DeleteAsync(Guid npiId, string npiLabel, Guid actorUserId, string actorName, CancellationToken cancellationToken)
        {
            Deleted.Add((npiId, npiLabel));
            return Task.CompletedTask;
        }
    }

    private static async Task<(FakeNpiRepository repo, Guid npiId)> NewDraftAsync()
    {
        var repo = new FakeNpiRepository();
        var id = await new CreateNpiDraftCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeUserDirectory())
            .HandleAsync(new CreateNpiDraftCommand(Guid.NewGuid(), "UsuarioComercial", "Comercial", new NpiCaptureDto(), Priority.Media, null), CancellationToken.None);
        return (repo, id);
    }

    [Fact]
    public async Task Only_the_admin_total_can_delete_and_must_type_the_folio()
    {
        var (repo, id) = await NewDraftAsync();
        var deletion = new RecordingDeletion();
        var handler = new DeleteNpiCommandHandler(repo, deletion);
        var admin = new CurrentUser(Guid.NewGuid(), "AdminTotal", AreaType.Total);

        var notAdmin = () => handler.HandleAsync(new DeleteNpiCommand(id, new CurrentUser(Guid.NewGuid(), "AdminComercial", AreaType.Comercial), "Admin Com", "BORRADOR"), CancellationToken.None);
        await notAdmin.Should().ThrowAsync<NotFoundException>();

        var wrong = () => handler.HandleAsync(new DeleteNpiCommand(id, admin, "Admin", "NPI-0001"), CancellationToken.None);
        (await wrong.Should().ThrowAsync<NpiValidationException>()).Which.Code.Should().Be("delete-confirmation-mismatch");
        deletion.Deleted.Should().BeEmpty();

        await handler.HandleAsync(new DeleteNpiCommand(id, admin, "Admin", " borrador "), CancellationToken.None);
        deletion.Deleted.Should().ContainSingle().Which.Should().Be((id, "BORRADOR"));
    }
}
